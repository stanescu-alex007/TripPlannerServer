using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.Infrastructure.Images;

/// <summary>
/// Production storage: a Cloudflare R2 bucket, spoken to through the S3 API and served
/// to browsers from a custom domain through Cloudflare's cache.
/// </summary>
public sealed class R2ImageStorage : IImageStorage
{
    /// <summary>
    /// Names are never reused, so a file never changes behind its URL: browsers and
    /// Cloudflare may keep it for a year without ever asking again.
    /// </summary>
    private const string CacheControl = "public, max-age=31536000, immutable";

    private readonly IAmazonS3 _s3;
    private readonly ImageOptions _options;
    private readonly ILogger<R2ImageStorage> _logger;

    public R2ImageStorage(IAmazonS3 s3, IOptions<ImageOptions> options, ILogger<R2ImageStorage> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(string folder, ProcessedImage image, CancellationToken ct = default)
    {
        var key = ImageKeys.NewKey(folder, image.Extension);

        using var body = new MemoryStream(image.Data);

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.R2.Bucket,
            Key = key,
            InputStream = body,
            ContentType = image.ContentType,
            Headers = { CacheControl = CacheControl },

            // R2 does not support the streaming signature the SDK uses by default.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        }, ct);

        return ImageKeys.ToUrl(_options.PublicBaseUrl, key);
    }

    public async Task DeleteAsync(string? url, CancellationToken ct = default)
    {
        var key = ImageKeys.FromUrl(_options.PublicBaseUrl, url);
        if (key is null) return;

        try
        {
            // Succeeds for a key that is already gone, so a retry is harmless.
            await _s3.DeleteObjectAsync(_options.R2.Bucket, key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image {Key}.", key);
        }
    }
}
