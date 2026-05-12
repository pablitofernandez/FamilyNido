using System.Text.RegularExpressions;
using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Features.Notifications;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.Families;
using FamilyNido.Domain.Identity;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Invitations;

/// <summary>
/// Slice: creates a one-time invitation token for a family member, optionally
/// creating the member in the same transaction. Sends the invitation email
/// best-effort and always returns the absolute "copy link" so the admin can
/// fall back to chat/whatsapp delivery if SMTP is unavailable.
/// </summary>
public static partial class CreateInvitation
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled)]
    private static partial Regex HexColorRegex();

    /// <summary>
    /// Command. Either reference an existing <paramref name="MemberId"/> (the
    /// member becomes the invitation target) or supply the four "new member"
    /// fields and let the handler create it inline.
    /// </summary>
    /// <param name="MemberId">Existing member to bind. When null, a new member is created.</param>
    /// <param name="DisplayName">Display name for the new member (required when <paramref name="MemberId"/> is null).</param>
    /// <param name="MemberType">Kind of member for the new row (required when creating; must be Adult — only adults authenticate).</param>
    /// <param name="ColorHex">Hex color for the new member.</param>
    /// <param name="BirthDate">Optional date of birth for the new member.</param>
    /// <param name="Email">Recipient email — also persisted on the new member as ContactEmail.</param>
    /// <param name="RoleOnAccept">Role the user receives upon acceptance. Adult or Admin.</param>
    public sealed record Command(
        Guid? MemberId,
        string? DisplayName,
        MemberType? MemberType,
        string? ColorHex,
        DateOnly? BirthDate,
        string Email,
        FamilyRole RoleOnAccept) : IRequest<Result<CreateInvitationResponse>>;

    /// <summary>Input validation.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator with all rules registered.</summary>
        public Validator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(254);

            RuleFor(x => x.RoleOnAccept)
                .Must(r => r == FamilyRole.Adult || r == FamilyRole.Admin)
                .WithMessage("RoleOnAccept must be Adult or Admin.");

            // When the caller wants to create a new member inline, all four
            // member-creation fields must be coherent.
            When(x => x.MemberId is null, () =>
            {
                RuleFor(x => x.DisplayName)
                    .NotEmpty()
                    .MaximumLength(120);

                RuleFor(x => x.MemberType)
                    .NotNull()
                    .Must(t => t == Domain.Families.MemberType.Adult)
                    .WithMessage("Only adult members can be invited.");

                RuleFor(x => x.ColorHex)
                    .NotEmpty()
                    .Matches(HexColorRegex())
                    .WithMessage("ColorHex must be in #RRGGBB format.");

                RuleFor(x => x.BirthDate)
                    .Must(d => d is null || d.Value <= DateOnly.FromDateTime(DateTime.UtcNow))
                    .WithMessage("BirthDate cannot be in the future.");
            });
        }
    }

    /// <summary>Handler — persists the invitation and dispatches the email.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<CreateInvitationResponse>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly IEmailSender _emailSender;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<Handler> _logger;
        private readonly EmailOptions _emailOptions;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            IEmailSender emailSender,
            TimeProvider timeProvider,
            IOptions<EmailOptions> emailOptions,
            ILogger<Handler> logger)
        {
            _db = db;
            _userContext = userContext;
            _emailSender = emailSender;
            _timeProvider = timeProvider;
            _logger = logger;
            _emailOptions = emailOptions.Value;
        }

        /// <inheritdoc />
        public async Task<Result<CreateInvitationResponse>> HandleAsync(Command request, CancellationToken ct)
        {
            var current = await _userContext.GetAsync(ct);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            // Resolve the target member: existing or just-created.
            FamilyMember member;
            if (request.MemberId is { } memberId)
            {
                var existing = await _db.FamilyMembers
                    .FirstOrDefaultAsync(m => m.Id == memberId && m.FamilyId == current.Family.Id, ct);
                if (existing is null)
                {
                    return ApplicationError.NotFound("family_member.not_found", $"Member {memberId} not found.");
                }

                if (existing.MemberType != Domain.Families.MemberType.Adult)
                {
                    return ApplicationError.Validation(
                        "invitation.member_not_adult",
                        "Only adult members can be invited.");
                }

                if (existing.UserId is not null)
                {
                    return ApplicationError.Conflict(
                        "family_member.already_linked",
                        "This member is already linked to a user.");
                }

                member = existing;
            }
            else
            {
                member = new FamilyMember
                {
                    FamilyId = current.Family.Id,
                    DisplayName = request.DisplayName!,
                    MemberType = request.MemberType!.Value,
                    ColorHex = request.ColorHex!,
                    BirthDate = request.BirthDate,
                    ContactEmail = normalizedEmail,
                };
                _db.FamilyMembers.Add(member);
            }

            // Single-use token: only the hash lives in the DB; the raw token
            // ships in the email and the response (so admins can copy the
            // link if SMTP is down).
            var rawToken = InvitationToken.GenerateRaw();
            var tokenHash = InvitationToken.Hash(rawToken);
            var now = _timeProvider.GetUtcNow();

            var invitation = new Invitation
            {
                FamilyId = current.Family.Id,
                FamilyMemberId = member.Id,
                FamilyMember = member,
                Email = normalizedEmail,
                RoleOnAccept = request.RoleOnAccept,
                TokenHash = tokenHash,
                ExpiresAt = now + _emailOptions.InvitationLifetime,
            };
            _db.Invitations.Add(invitation);

            await _db.SaveChangesAsync(ct);

            // Build the absolute link from configured AppBaseUrl. We rely on
            // the operator to keep the URL aligned with the front-facing
            // domain — there is no good way to derive it from the request
            // (the API may live behind several reverse proxies). The path
            // matches the Angular route `/invite/:token` (renamed from
            // `/invitar/` when we Englishified the routes).
            var copyLink = $"{_emailOptions.AppBaseUrl.TrimEnd('/')}/invite/{rawToken}";

            // Best-effort email. Even if delivery fails we keep the
            // invitation alive — the admin can copy the link from the
            // response and pass it manually.
            var subject = $"Te han invitado a la familia {current.Family.Name} en FamilyNido";
            var html = RenderHtml(current.Family.Name, current.User.DisplayName, member.DisplayName, copyLink, invitation.ExpiresAt);
            EmailResult emailResult;
            try
            {
                emailResult = await _emailSender.SendAsync(new EmailMessage(normalizedEmail, subject, html), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email send unexpectedly threw for invitation {InvitationId}", invitation.Id);
                emailResult = new EmailResult(false, null, "exception");
            }

            var dto = ToDto(invitation, member.DisplayName);
            return new CreateInvitationResponse(
                Invitation: dto,
                MemberId: member.Id,
                CopyLink: copyLink,
                EmailDelivered: emailResult.Delivered);
        }

        private static InvitationDto ToDto(Invitation i, string memberDisplayName) => new(
            Id: i.Id,
            FamilyMemberId: i.FamilyMemberId,
            MemberDisplayName: memberDisplayName,
            Email: i.Email,
            RoleOnAccept: i.RoleOnAccept,
            ExpiresAt: i.ExpiresAt,
            CreatedAt: i.CreatedAt);

        private static string RenderHtml(
            string familyName,
            string inviterName,
            string memberDisplayName,
            string copyLink,
            DateTimeOffset expiresAt)
        {
            // Keep it minimal: a single paragraph, the link as a button-like
            // anchor, and an expiration line. No external assets.
            var safeFamily = WebUtility(familyName);
            var safeInviter = WebUtility(inviterName);
            var safeMember = WebUtility(memberDisplayName);
            var safeLink = WebUtility(copyLink);
            var expires = expiresAt.ToString("d MMMM yyyy");

            return $$"""
                <html>
                <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #2b1d12;">
                    <p>Hola {{safeMember}},</p>
                    <p><strong>{{safeInviter}}</strong> te ha invitado a unirte a la familia <strong>{{safeFamily}}</strong> en FamilyNido.</p>
                    <p>
                        <a href="{{safeLink}}" style="background:#C96442;color:#fff;text-decoration:none;padding:10px 18px;border-radius:8px;">
                            Aceptar invitación
                        </a>
                    </p>
                    <p style="color: #6b5f55; font-size: 13px;">
                        O copia y pega este enlace en tu navegador:<br>
                        <a href="{{safeLink}}">{{safeLink}}</a>
                    </p>
                    <p style="color: #6b5f55; font-size: 13px;">El enlace caduca el {{expires}}.</p>
                </body>
                </html>
                """;
        }

        // Tiny escape for the small set of fields we interpolate. Avoids
        // pulling in a full HTML library for one email template.
        private static string WebUtility(string s) => System.Net.WebUtility.HtmlEncode(s);
    }
}
