using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;

namespace FamilyNido.Api.Features.Weather;

/// <summary>Endpoint exposing the dashboard weather widget data.</summary>
public static class WeatherEndpoints
{
    /// <summary>Registers <c>GET /api/weather/today</c>.</summary>
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/weather").WithTags("Weather");
        group.MapGet("/today", GetTodayAsync).RequireAuthorization(Policies.AuthenticatedUser);
        return app;
    }

    private static async Task<IResult> GetTodayAsync(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetWeatherToday.Query(), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }
}
