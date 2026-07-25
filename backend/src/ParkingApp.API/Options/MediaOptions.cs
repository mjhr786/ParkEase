namespace ParkingApp.API.Options;

/// <summary>
/// Media / image handling knobs for free-tier hosting + Cloudflare R2 free.
/// Bound from configuration section <c>Media</c>.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>
    /// When true, <see cref="Middleware.ImageResizingMiddleware"/> may resize local
    /// <c>/uploads</c> files via <c>?w=</c>/<c>?h=</c>.
    /// Default <c>false</c>: production uses R2 public URLs (no app-server resize),
    /// and free hosts should not spend CPU on Skia.
    /// Set true only if you still serve local disk uploads and need on-the-fly resize.
    /// </summary>
    public bool EnableRuntimeResize { get; set; } = false;

    /// <summary>
    /// Cap on runtime resize width/height when <see cref="EnableRuntimeResize"/> is true
    /// (prevents abuse / huge decode jobs). Defaults to 800.
    /// </summary>
    public int MaxRuntimeResizeDimension { get; set; } = 800;

    /// <summary>
    /// Reserved for a future optional single upload-time thumbnail.
    /// Default <c>false</c> on free Cloudflare R2: extra PutObject calls and storage
    /// would count against free Class A / GB limits. Full public R2 URLs remain the
    /// supported path (list/map already use the first image URL only).
    /// </summary>
    public bool GenerateUploadThumbnail { get; set; } = false;

    /// <summary>Max width for an upload-time thumb if generation is ever enabled.</summary>
    public int UploadThumbnailMaxWidth { get; set; } = 400;
}
