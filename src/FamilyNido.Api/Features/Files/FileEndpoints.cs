using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;

namespace FamilyNido.Api.Features.Files;

/// <summary>REST endpoints for the shared file-asset module.</summary>
public static class FileEndpoints
{
    /// <summary>Registers <c>/api/files</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/files")
            .WithTags("Files")
            .RequireAuthorization(Policies.AuthenticatedUser);

        // multipart/form-data: single "file" part + optional "module" (default "wall").
        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/{id:guid}", DownloadAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { code = "file.missing_multipart", message = "Upload must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { code = "file.missing", message = "No file provided." });
        }

        var module = form["module"].ToString();
        if (string.IsNullOrWhiteSpace(module))
        {
            module = "wall";
        }

        await using var stream = file.OpenReadStream();
        var command = new UploadFile.Command(stream, file.ContentType, file.Length, module);
        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Created(result.Value.Url, result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DownloadAsync(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DownloadFile.Query(id), ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToHttpResult();
        }

        var payload = result.Value;
        // Results.Stream disposes the stream after writing; caches for 1 day —
        // fine because the URL carries the id and file content is immutable once written.
        return Results.Stream(
            payload.Stream,
            contentType: payload.ContentType,
            enableRangeProcessing: true);
    }
}
