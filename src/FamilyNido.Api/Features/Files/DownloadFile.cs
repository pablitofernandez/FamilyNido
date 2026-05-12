using FamilyNido.Api.Features.Auth;
using FamilyNido.Api.Options;
using FamilyNido.Api.Shared.Errors;
using FamilyNido.Api.Shared.Mediator;
using FamilyNido.Api.Shared.Outcomes;
using FamilyNido.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FamilyNido.Api.Features.Files;

/// <summary>
/// Slice: open the bytes of a <see cref="Domain.Files.FileAsset"/> for streaming back
/// to an authorized caller. Enforces per-family scoping: the asset must belong to the
/// caller's family (404 otherwise — indistinguishable from "does not exist").
/// </summary>
public static class DownloadFile
{
    /// <summary>Resolved payload delivered to the endpoint to stream back.</summary>
    /// <param name="Stream">Open readable stream; disposed by the endpoint after sending.</param>
    /// <param name="ContentType">MIME type to set on the response.</param>
    /// <param name="SizeBytes">Size of the stream in bytes.</param>
    public sealed record FilePayload(Stream Stream, string ContentType, long SizeBytes);

    /// <summary>Query carrying the target id.</summary>
    public sealed record Query(Guid FileId) : IRequest<Result<FilePayload>>;

    /// <summary>Handler — looks up the asset and opens a read stream on disk.</summary>
    public sealed class Handler : IRequestHandler<Query, Result<FilePayload>>
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserContext _userContext;
        private readonly FilesOptions _options;

        /// <summary>Primary constructor.</summary>
        public Handler(
            ApplicationDbContext db,
            ICurrentUserContext userContext,
            IOptions<FilesOptions> options)
        {
            _db = db;
            _userContext = userContext;
            _options = options.Value;
        }

        /// <inheritdoc />
        public async Task<Result<FilePayload>> HandleAsync(Query request, CancellationToken cancellationToken)
        {
            var current = await _userContext.GetAsync(cancellationToken);
            if (current is null)
            {
                return ApplicationError.Forbidden("auth.not_linked", "Caller is not linked to a family member.");
            }

            var asset = await _db.FileAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.Id == request.FileId && a.FamilyId == current.Family.Id,
                    cancellationToken);

            if (asset is null)
            {
                return ApplicationError.NotFound("file.not_found", $"File {request.FileId} not found.");
            }

            var absolutePath = Path.Combine(_options.StorageRoot, asset.RelativePath);
            if (!File.Exists(absolutePath))
            {
                return ApplicationError.NotFound(
                    "file.bytes_missing",
                    $"File {request.FileId} metadata exists but bytes are missing on disk.");
            }

            var stream = File.OpenRead(absolutePath);
            return new FilePayload(stream, asset.ContentType, asset.SizeBytes);
        }
    }
}
