using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>
/// Slice: clears a member's photo. Mirrors the auth rules of
/// <see cref="UploadMemberPhoto"/> (admin OR self) and removes both the row
/// pointer (PhotoPath) and the underlying file on disk so the avatar
/// pipeline falls back to initials.
/// </summary>
public static class RemoveMemberPhoto
{
    /// <summary>Command.</summary>
    /// <param name="MemberId">Target member id.</param>
    public sealed record Command(Guid MemberId) : IRequest<Result<FamilyMemberDto>>;

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

            var isAdmin = current.User.Role == FamilyRole.Admin;
            var isSelf = current.Member.Id == member.Id;
            if (!isAdmin && !isSelf)
            {
                return ApplicationError.Forbidden(
                    "family_member.not_self_or_admin",
                    "Only an admin or the member themself can remove this photo.");
            }

            if (!string.IsNullOrEmpty(member.PhotoPath))
            {
                var fullPath = Path.Combine(_filesOptions.StorageRoot, member.PhotoPath);
                // Best-effort delete: if the file is already gone (manual cleanup,
                // volume rotated, …) we still clear the DB pointer.
                try
                {
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                    }
                }
                catch
                {
                    // Swallow — clearing the row matters more than removing the file.
                }
            }

            member.PhotoPath = null;
            await _db.SaveChangesAsync(ct);

            return FamilyMemberDto.From(member);
        }
    }
}
