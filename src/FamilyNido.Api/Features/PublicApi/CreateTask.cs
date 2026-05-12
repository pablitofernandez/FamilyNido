using System.Security.Claims;
using FamilyNido.Api.Features.Integrations;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Domain.HouseholdTasks;
using FamilyNido.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace FamilyNido.Api.Features.PublicApi;

/// <summary>
/// Slice for <c>POST /api/v1/tasks</c>. Authenticated via an integration API key
/// (<see cref="IntegrationApiKeyDefaults.AuthenticationScheme"/>); creates a
/// <see cref="HouseholdTask"/> in the family the token belongs to. The caller
/// (n8n, IFTTT, an iOS shortcut, …) decides the title, category and points
/// instead of the server hard-coding a per-appliance taxonomy.
/// </summary>
public static class CreateTask
{
    /// <summary>Request body. All non-required fields use safe defaults.</summary>
    /// <param name="Title">Required, max 200 chars.</param>
    /// <param name="Category">Optional, max 50 chars. Defaults to the domain default ("General").</param>
    /// <param name="Points">Optional, 0..100 (clamped by validator). Defaults to the domain default (5).</param>
    /// <param name="DueDate">Optional target date; ignored when <paramref name="IsFloating"/> is true.</param>
    /// <param name="IsFloating">Optional; when true, the task is floating (no fixed date).</param>
    /// <param name="ResponsibleMemberId">Optional; must belong to the same family as the token.</param>
    /// <param name="Deduplicate">Optional; when true, skips creation if a pending task with the same title already exists.</param>
    public sealed record Command(
        string Title,
        string? Category,
        int? Points,
        DateOnly? DueDate,
        bool IsFloating,
        Guid? ResponsibleMemberId,
        bool Deduplicate) : IRequest<Result<Response>>;

    /// <summary>Response body returned to the integration caller.</summary>
    /// <param name="Created">True when a brand-new task row was inserted.</param>
    /// <param name="Reason">Optional machine-readable detail when <paramref name="Created"/> is false.</param>
    /// <param name="TaskId">Id of the (existing or new) task.</param>
    /// <param name="Title">Human-readable task title.</param>
    public sealed record Response(bool Created, string? Reason, Guid TaskId, string Title);

    /// <summary>Input validation. Keeps the handler focused on business rules.</summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>Creates the validator.</summary>
        public Validator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Category)
                .MaximumLength(50)
                .When(x => x.Category is not null);

            RuleFor(x => x.Points)
                .InclusiveBetween(0, 100)
                .When(x => x.Points.HasValue);
        }
    }

    /// <summary>Handler: validates the family/member, optionally dedups, then inserts.</summary>
    public sealed class Handler : IRequestHandler<Command, Result<Response>>
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly TimeProvider _timeProvider;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            IHttpContextAccessor httpContextAccessor,
            TimeProvider timeProvider)
        {
            _db = db;
            _httpContextAccessor = httpContextAccessor;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc />
        public async Task<Result<Response>> HandleAsync(Command request, CancellationToken cancellationToken)
        {
            var principal = _httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("HttpContext is required for integration auth resolution.");

            // Claims are stamped by IntegrationApiKeyAuthenticationHandler on
            // every successful API-key authentication. Their absence here would
            // mean the endpoint was wired up with the wrong policy.
            var familyIdClaim = principal.FindFirstValue(IntegrationClaimTypes.FamilyId);
            var authorMemberIdClaim = principal.FindFirstValue(IntegrationClaimTypes.AuthorMemberId);
            if (!Guid.TryParse(familyIdClaim, out var familyId)
                || !Guid.TryParse(authorMemberIdClaim, out var authorMemberId))
            {
                return ApplicationError.Forbidden(
                    "integration.bad_principal",
                    "Integration principal is missing required claims.");
            }

            // Responsible member, when supplied, must belong to the same family
            // as the token. Otherwise an integration in family A could pin a
            // chore on a member of family B — the integration auth scope is
            // single-family on purpose.
            if (request.ResponsibleMemberId is { } responsibleId)
            {
                var responsibleExists = await _db.FamilyMembers
                    .AsNoTracking()
                    .AnyAsync(
                        m => m.Id == responsibleId && m.FamilyId == familyId,
                        cancellationToken);
                if (!responsibleExists)
                {
                    return ApplicationError.Validation(
                        "public_api.responsible_member_not_in_family",
                        "responsibleMemberId does not belong to the token's family.");
                }
            }

            // Opt-in dedup: same rule as the legacy HA endpoint, now exposed as
            // a flag so generic callers can choose whether they want it. A task
            // counts as "still pending" when it has no completions and isn't
            // archived; floating chores graduate forever on their first
            // completion, so this is the right pending-now signal.
            if (request.Deduplicate)
            {
                var pending = await _db.HouseholdTasks
                    .Where(t => t.FamilyId == familyId
                        && !t.IsArchived
                        && t.Title == request.Title
                        && !t.Completions.Any())
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pending is not null)
                {
                    return new Response(false, "already-pending", pending.Id, pending.Title);
                }
            }

            var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().Date);

            // Floating tasks: no fixed DueDate, recurrence forced to None so the
            // floating "always pending" semantics aren't crossed with a weekly
            // pattern. Non-floating tasks default DueDate to today when the
            // caller didn't supply one, which matches the legacy HA behaviour.
            var task = new HouseholdTask
            {
                FamilyId = familyId,
                Title = request.Title,
                Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category,
                Points = request.Points ?? 5,
                Recurrence = RecurrenceMode.None,
                IsFloating = request.IsFloating,
                StartDate = today,
                DueDate = request.IsFloating ? null : (request.DueDate ?? today),
                CreatedByMemberId = authorMemberId,
                ResponsibleMemberId = request.ResponsibleMemberId,
            };

            _db.HouseholdTasks.Add(task);
            await _db.SaveChangesAsync(cancellationToken);

            return new Response(true, null, task.Id, task.Title);
        }
    }
}
