using FamilyNido.Domain.Common;
using FamilyNido.Domain.Families;

namespace FamilyNido.Domain.Files;

/// <summary>
/// Binary asset (image, PDF…) owned by a family and stored on disk. Records the
/// authenticated metadata needed to serve the bytes back safely: owning family,
/// uploader, relative path under the configured storage root, content type and
/// size. Used initially for wall images; reused later by health and recipes.
/// </summary>
public sealed class FileAsset : AuditableEntity
{
    /// <summary>Family this asset belongs to (authorization boundary).</summary>
    public required Guid FamilyId { get; set; }

    /// <summary>Navigation to the owning <see cref="Family"/>.</summary>
    public Family? Family { get; set; }

    /// <summary>Member who uploaded the file. Retained for audit / attribution.</summary>
    public required Guid OwnerMemberId { get; set; }

    /// <summary>Navigation to the uploader.</summary>
    public FamilyMember? OwnerMember { get; set; }

    /// <summary>
    /// Path relative to the storage root (e.g. <c>wall/2026/04/&lt;uuid&gt;.jpg</c>).
    /// Unique across the table so an asset record always maps to exactly one file on disk.
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>MIME type of the stored content (e.g. <c>image/jpeg</c>).</summary>
    public required string ContentType { get; set; }

    /// <summary>Size of the stored content in bytes.</summary>
    public required long SizeBytes { get; set; }

    /// <summary>Image width in pixels, if known. Null for non-image or undetected.</summary>
    public int? Width { get; set; }

    /// <summary>Image height in pixels, if known. Null for non-image or undetected.</summary>
    public int? Height { get; set; }
}
