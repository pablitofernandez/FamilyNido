using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>
/// Slice: a member's profile photo. The endpoint accepts any of the
/// images allowed by <see cref="FilesOptions"/>, decodes them with
/// ImageSharp, crops them to a 512×512 square and re-encodes as JPEG so
/// every avatar has a uniform shape and size on disk regardless of source.
/// Reused by both the admin (any member) and self (the linked user)
/// permissions paths.
/// </summary>
public static class UploadMemberPhoto
{
    /// <summary>Final stored size for every avatar (square).</summary>
    public const int OutputSize = 512;

    /// <summary>JPEG quality for the re-encoded avatar.</summary>
    private const int JpegQuality = 88;

    /// <summary>Subfolder under the files volume where avatars live.</summary>
    private const string AvatarsFolder = "members";

    /// <summary>Command.</summary>
    /// <param name="MemberId">Target member id.</param>
    /// <param name="Stream">Input image stream (raw bytes from multipart).</param>
    /// <param name="ContentType">Reported MIME type from the multipart part.</param>
    /// <param name="LengthBytes">Reported byte count from the multipart part.</param>
    public sealed record Command(
        Guid MemberId,
        Stream Stream,
        string ContentType,
        long LengthBytes) : IRequest<Result<FamilyMemberDto>>;

    /// <summary>Handler.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<FamilyMemberDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly FilesOptions _filesOptions;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            IOptions<FilesOptions> filesOptions)
        {
            _db = db;
            _userContext = userContext;
            _filesOptions = filesOptions.Value;
        }

        /// <inheritdoc />
        public async Task<Result<FamilyMemberDto>> HandleAsync(Command request, CancellationToken ct)
        {
            var current = await _userContext.GetAsync(ct);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var member = await _db.FamilyMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == request.MemberId && m.FamilyId == current.Family.Id, ct);
            if (member is null)
            {
                return ApplicationError.NotFound("family_member.not_found", "Member not found.");
            }

            // Authorization: the admin can change anyone's photo; a regular user
            // can only change their *own* member's photo.
            var isAdmin = current.User.Role == FamilyRole.Admin;
            var isSelf = current.Member.Id == member.Id;
            if (!isAdmin && !isSelf)
            {
                return ApplicationError.Forbidden(
                    "family_member.not_self_or_admin",
                    "Only an admin or the member themself can change this photo.");
            }

            // MIME + size guards. We trust the multipart-supplied content-type
            // here (it's checked again by ImageSharp when decoding); a wrong
            // type would also fail to decode and surface a validation error.
            if (request.LengthBytes <= 0)
            {
                return ApplicationError.Validation("photo.empty", "Empty file.");
            }
            if (request.LengthBytes > _filesOptions.MaxImageBytes)
            {
                return ApplicationError.Validation("photo.too_large", "File exceeds the configured size limit.");
            }
            if (!_filesOptions.AllowedImageTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return ApplicationError.Validation("photo.unsupported_type", $"Content type '{request.ContentType}' not supported.");
            }

            // Decode → resize+crop center → JPEG. ImageSharp throws on malformed
            // input; the endpoint wrapper catches and surfaces a clean 400.
            using var image = await Image.LoadAsync(request.Stream, ct);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Size = new Size(OutputSize, OutputSize),
                Sampler = KnownResamplers.Lanczos3,
            }));

            // Persist as members/{memberId}.jpg, overwriting any previous photo
            // so we never accumulate stale files. The path is deterministic so
            // the static-file middleware can cache it (and busts via the entity
            // tag when the bytes change).
            var folder = Path.Combine(_filesOptions.StorageRoot, AvatarsFolder);
            Directory.CreateDirectory(folder);
            var fileName = $"{member.Id:N}.jpg";
            var fullPath = Path.Combine(folder, fileName);

            await using (var output = File.Create(fullPath))
            {
                var encoder = new JpegEncoder { Quality = JpegQuality };
                await image.SaveAsync(output, encoder, ct);
            }

            member.PhotoPath = $"{AvatarsFolder}/{fileName}";
            await _db.SaveChangesAsync(ct);

            return FamilyMemberDto.From(member);
        }
    }
}
