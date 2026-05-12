using FamilyNido.Domain.Files;

namespace FamilyNido.Api.Features.Files;

/// <summary>Read-model projection of a <see cref="FileAsset"/> returned by the API.</summary>
/// <param name="Id">Stable asset identifier.</param>
/// <param name="ContentType">MIME type of the stored bytes.</param>
/// <param name="SizeBytes">Size of the stored bytes.</param>
/// <param name="Width">Image width in pixels, if known.</param>
/// <param name="Height">Image height in pixels, if known.</param>
/// <param name="OwnerMemberId">Member who uploaded the file.</param>
/// <param name="Url">Relative URL the Angular client uses to load the bytes.</param>
public sealed record FileAssetDto(
    Guid Id,
    string ContentType,
    long SizeBytes,
    int? Width,
    int? Height,
    Guid OwnerMemberId,
    string Url)
{
    /// <summary>Project a domain entity to the DTO shape used by the API.</summary>
    public static FileAssetDto From(FileAsset a) => new(
        Id: a.Id,
        ContentType: a.ContentType,
        SizeBytes: a.SizeBytes,
        Width: a.Width,
        Height: a.Height,
        OwnerMemberId: a.OwnerMemberId,
        Url: $"/api/files/{a.Id}");
}
