using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;

namespace FamilyNido.Api.Features.Scores;

/// <summary>HTTP surface of the family scoreboard module.</summary>
public static class ScoreEndpoints
{
    /// <summary>Registers <c>/api/scores/*</c> routes.</summary>
    public static IEndpointRouteBuilder MapScoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scores").WithTags("Scores");

        group.MapGet("/leaderboard", GetLeaderboardAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapGet("/members/{memberId:guid}", GetMemberScoreAsync).RequireAuthorization(Policies.AuthenticatedUser);
        group.MapGet("/members/{memberId:guid}/history", GetMemberHistoryAsync).RequireAuthorization(Policies.AuthenticatedUser);

        return app;
    }

    private static async Task<IResult> GetLeaderboardAsync(
        DateOnly from,
        DateOnly to,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetScoreboard.Query(from, to), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetMemberScoreAsync(
        Guid memberId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetMemberScore.Query(memberId), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetMemberHistoryAsync(
        Guid memberId,
        int? limit,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(
            new GetMemberCompletionHistory.Query(memberId, limit ?? GetMemberCompletionHistory.DefaultLimit), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}
