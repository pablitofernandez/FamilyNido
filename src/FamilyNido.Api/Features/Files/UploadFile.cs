using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Files;
using FamilyNido.Persistence;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Files;

/// <summary>
/// Slice: persist an uploaded image to the configured storage root + create a
/// <see cref="FileAsset"/> metadata row. Files are scoped per family (used later
/// to authorize downloads) and placed under a module-specific subfolder
/// (<c>wall/yyyy/MM/&lt;uuid&gt;.ext</c>). First consumer is the wall module; the
/// same endpoint will back recipes and health attachments later on.
/// </summary>
public static class UploadFile
{
    /// <summary>Command carrying the raw stream + metadata.</summary>
    /// <param name="Stream">Open readable stream positioned at the start of the file.</param>
    /// <param name="ContentType">MIME type as declared by the client.</param>
    /// <param name="SizeBytes">Size of the stream in bytes.</param>
    /// <param name="Module">Logical folder under the storage root (e.g. <c>wall</c>).</param>
    public sealed record Command(
        Stream Stream,
        string ContentType,
        long SizeBytes,
        string Module) : IRequest<Result<FileAssetDto>>;

    /// <summary>Handler — writes to disk then persists the asset row.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<FileAssetDto>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly FilesOptions _options;
        private readonly TimeProvider _clock;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            IOptions<FilesOptions> options,
            TimeProvider clock)
        {
            _db = db;
            _userContext = userContext;
            _options = options.Value;
            _clock = clock;
        }

        /// <inheritdoc />
        public async Task<Result<FileAssetDto>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            if (!_options.AllowedImageTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return ApplicationError.Validation(
                    "file.unsupported_content_type",
                    $"Content type '{request.ContentType}' is not accepted.");
            }

            if (request.SizeBytes <= 0 || request.SizeBytes > _options.MaxImageBytes)
            {
                return ApplicationError.Validation(
                    "file.size_out_of_range",
                    $"File size must be between 1 byte and {_options.MaxImageBytes} bytes.");
            }

            var assetId = Guid.CreateVersion7();
            var now = _clock.GetUtcNow();
            var extension = ExtensionFor(request.ContentType);
            var relativePath = $"{request.Module}/{now.Year:D4}/{now.Month:D2}/{assetId}{extension}";
            var absolutePath = Path.Combine(_options.StorageRoot, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

            await using (var fs = File.Create(absolutePath))
            {
                await request.Stream.CopyToAsync(fs, cancellationToken);
            }

            var asset = new FileAsset
            {
                Id = assetId,
                FamilyId = current.Family.Id,
                OwnerMemberId = current.Member.Id,
                RelativePath = relativePath,
                ContentType = request.ContentType,
                SizeBytes = request.SizeBytes,
            };

            _db.FileAssets.Add(asset);
            await _db.SaveChangesAsync(cancellationToken);

            return FileAssetDto.From(asset);
        }

        private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            _ => string.Empty,
        };
    }
}
