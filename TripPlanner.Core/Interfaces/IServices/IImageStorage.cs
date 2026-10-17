namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Where processed images live and the public URL they are served from. Swapped by
    /// configuration (<c>Images:Provider</c>): a local folder in development, an
    /// S3-compatible bucket (Cloudflare R2) in production.
    /// </summary>
    public interface IImageStorage
    {
        /// <summary>
        /// Stores the bytes under a fresh random name inside <paramref name="folder"/> and
        /// returns the public URL. Names are never reused, so a URL can be cached forever.
        /// </summary>
        Task<string> SaveAsync(string folder, ProcessedImage image, CancellationToken ct = default);

        /// <summary>
        /// Removes the image behind <paramref name="url"/>. URLs this storage did not issue —
        /// links pasted before uploads existed — are ignored, as is an image already gone.
        /// Never throws: failures are logged. A leftover file is harmless; failing a request
        /// whose new image was already saved is not.
        /// </summary>
        Task DeleteAsync(string? url, CancellationToken ct = default);
    }

    public static class ImageUpload
    {
        /// <summary>Largest file accepted. Phones produce 5–15 MB; the browser shrinks most first anyway.</summary>
        public const long MaxFileBytes = 20 * 1024 * 1024;

        /// <summary>The whole multipart request: the file plus its headers and boundaries.</summary>
        public const long MaxRequestBytes = MaxFileBytes + 1024 * 1024;
    }

    /// <summary>Top-level folders in storage, one per kind of image.</summary>
    public static class ImageFolders
    {
        public const string Avatars = "avatars";
        public const string TripCovers = "trips";
    }

    /// <summary>An image ready to store: already resized, re-encoded and stripped of metadata.</summary>
    public sealed record ProcessedImage(byte[] Data, string ContentType, string Extension);
}
