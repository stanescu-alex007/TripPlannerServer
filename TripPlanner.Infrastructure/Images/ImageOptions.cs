namespace TripPlanner.Infrastructure.Images;

public sealed class ImageOptions
{
    public const string SectionName = "Images";

    /// <summary>"R2" for the Cloudflare bucket, "Local" to write to a folder in dev.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>
    /// Where stored images are served from, without a trailing slash. Every URL handed
    /// out starts with it, which is also how a URL is recognised as ours on delete.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5050/uploads";

    public LocalOptions Local { get; set; } = new();
    public R2Options R2 { get; set; } = new();

    public sealed class LocalOptions
    {
        public string Directory { get; set; } = "../_uploads";
    }

    public sealed class R2Options
    {
        /// <summary>https://&lt;account-id&gt;.r2.cloudflarestorage.com</summary>
        public string ServiceUrl { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string AccessKeyId { get; set; } = string.Empty;
        public string SecretAccessKey { get; set; } = string.Empty;
    }
}
