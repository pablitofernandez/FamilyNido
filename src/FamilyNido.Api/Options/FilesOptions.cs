namespace FamilyNido.Api.Options;

/// <summary>
/// Configuration for the file-asset storage used by the wall and (future) other
/// modules that need to attach binary content (health records, recipes, etc.).
/// Files are stored under <see cref="StorageRoot"/> with a module-scoped subfolder
/// (e.g. <c>wall/YYYY/MM/&lt;uuid&gt;.jpg</c>) and served back through authenticated
/// API endpoints — never directly via the web server — so per-family authorization
/// can run on every access.
/// </summary>
public sealed class FilesOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Files";

    /// <summary>
    /// Absolute directory where files are persisted. Mapped to a Docker volume
    /// in prod (<c>familynido-files:/app/data/files</c> in
    /// <c>deploy/docker-compose.prod.yml</c>); overridden to a local relative
    /// path in dev via <c>appsettings.Development.json</c>.
    /// </summary>
    public string StorageRoot { get; init; } = "/app/data/files";

    /// <summary>Maximum accepted size for an uploaded image, in bytes. Defaults to 10 MB.</summary>
    public long MaxImageBytes { get; init; } = 10L * 1024 * 1024;

    /// <summary>
    /// Accepted MIME types for image uploads. Includes HEIC/HEIF so iOS Photos
    /// uploads work without forcing the user to switch the camera format to
    /// "Most Compatible". Browsers other than Safari may not render HEIC inline,
    /// but at least the upload itself does not get rejected at the API boundary.
    /// </summary>
    public string[] AllowedImageTypes { get; init; } =
        ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];
}
