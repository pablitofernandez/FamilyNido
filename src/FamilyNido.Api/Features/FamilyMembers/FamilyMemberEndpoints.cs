using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Http;
using FamilyNido.Api.Shared.Mediator;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.FamilyMembers;

/// <summary>REST endpoints for managing family members (RF-USR-*).</summary>
public static class FamilyMemberEndpoints
{
    /// <summary>Registers <c>/api/family-members</c> endpoints on the given route group.</summary>
    public static IEndpointRouteBuilder MapFamilyMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/family-members")
            .WithTags("FamilyMembers")
            .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(Policies.Admin);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(Policies.Admin);
        group.MapPatch("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(Policies.Admin);
        group.MapPatch("/{id:guid}/activate", ActivateAsync).RequireAuthorization(Policies.Admin);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Policies.Admin);

        // Profile photo: any authenticated user can read the bytes; the
        // upload/delete handlers run their own admin-OR-self check inside.
        group.MapGet("/{id:guid}/photo", DownloadPhotoAsync);
        group.MapPost("/{id:guid}/photo", UploadPhotoAsync).DisableAntiforgery();
        group.MapDelete("/{id:guid}/photo", RemovePhotoAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        bool? includeInactive,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ListFamilyMembers.Query(includeInactive ?? false), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.SendAsync(new GetFamilyMember.Query(id), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> CreateAsync(
        CreateFamilyMember.Command command,
        IValidator<CreateFamilyMember.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Created($"/api/family-members/{result.Value.Id}", result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateFamilyMemberBody body,
        IValidator<UpdateFamilyMember.Command> validator,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateFamilyMember.Command(id, body.DisplayName, body.ColorHex, body.BirthDate, body.ContactEmail);

        var validation = await validator.ValidateOrProblemAsync(command, ct);
        if (validation is not null)
        {
            return validation;
        }

        var result = await mediator.SendAsync(command, ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> ActivateAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new ActivateFamilyMember.Command(id), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeactivateAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeactivateFamilyMember.Command(id), ct);
        return result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new DeleteFamilyMember.Command(id), ct);
        return result.IsSuccess
            ? Results.NoContent()
            : result.Error.ToHttpResult();
    }

    private static async Task<IResult> UploadPhotoAsync(
        Guid id,
        HttpRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { code = "photo.missing_multipart", message = "Upload must be multipart/form-data." });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { code = "photo.missing", message = "No file provided." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var command = new UploadMemberPhoto.Command(id, stream, file.ContentType, file.Length);
            var result = await mediator.SendAsync(command, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
        }
        catch (SixLabors.ImageSharp.UnknownImageFormatException)
        {
            return Results.BadRequest(new { code = "photo.unsupported_type", message = "File could not be decoded as a supported image." });
        }
        catch (SixLabors.ImageSharp.InvalidImageContentException)
        {
            return Results.BadRequest(new { code = "photo.corrupt", message = "Image data is malformed." });
        }
    }

    private static async Task<IResult> RemovePhotoAsync(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new RemoveMemberPhoto.Command(id), ct);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToHttpResult();
    }

    private static async Task<IResult> DownloadPhotoAsync(
        Guid id,
        IMediator mediator,
        IOptions<FilesOptions> filesOptions,
        CancellationToken ct)
    {
        // Resolve the row through the standard read slice so cross-family
        // access is rejected (the slice already filters by the caller's
        // family). We then stream the file from disk.
        var result = await mediator.SendAsync(new GetFamilyMember.Query(id), ct);
        if (!result.IsSuccess)
        {
            return result.Error.ToHttpResult();
        }

        var member = result.Value;
        if (string.IsNullOrEmpty(member.PhotoPath))
        {
            return Results.NotFound();
        }

        // Resolve to an absolute path. Results.File interprets relative paths
        // against the IWebHostEnvironment.WebRootFileProvider (i.e. wwwroot),
        // which silently breaks when StorageRoot is configured as a relative
        // path like "./data/uploads" — the default in Development.
        var fullPath = Path.GetFullPath(
            Path.Combine(filesOptions.Value.StorageRoot, member.PhotoPath));
        if (!File.Exists(fullPath))
        {
            // The pointer survived but the file is gone (manual cleanup,
            // volume rotation, …). Returning 404 keeps the avatar pipeline
            // falling back to initials.
            return Results.NotFound();
        }

        return Results.File(fullPath, contentType: "image/jpeg", enableRangeProcessing: true);
    }
}

/// <summary>
/// Wire-level payload for PUT /api/family-members/{id}. The route parameter
/// carries the target id, so the body only carries the mutable fields.
/// </summary>
/// <param name="DisplayName">Name shown in the UI. 1-120 chars.</param>
/// <param name="ColorHex">Hex color (#RRGGBB).</param>
/// <param name="BirthDate">Optional date of birth.</param>
/// <param name="ContactEmail">Optional informational contact email.</param>
public sealed record UpdateFamilyMemberBody(
    string DisplayName,
    string ColorHex,
    DateOnly? BirthDate,
    string? ContactEmail);
