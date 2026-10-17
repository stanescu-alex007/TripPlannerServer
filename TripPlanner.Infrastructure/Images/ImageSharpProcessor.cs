using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.Infrastructure.Images;

public sealed class ImageSharpProcessor : IImageProcessor
{
    private const int AvatarSize = 512;
    private const int CoverMaxSide = 1600;

    /// <summary>
    /// Header dimensions are checked before any pixel is decoded: a few-KB file can claim
    /// to be 60000×60000 and would otherwise take gigabytes of memory to open.
    /// </summary>
    private const long MaxPixels = 50_000_000;

    /// <summary>Only formats a browser shows and a phone produces. No GIF, BMP, TIFF or SVG.</summary>
    private static readonly DecoderOptions Decoder = new()
    {
        Configuration = new Configuration(
            new JpegConfigurationModule(),
            new PngConfigurationModule(),
            new WebpConfigurationModule())
    };

    private static readonly WebpEncoder Encoder = new() { Quality = 80 };

    /// <summary>
    /// A decoded 12 MP photo holds ~50 MB of pixels. Two at a time keeps a burst of
    /// uploads from exhausting the server's memory; the rest wait their turn.
    /// </summary>
    private static readonly SemaphoreSlim Gate = new(2);

    private readonly ILogger<ImageSharpProcessor> _logger;

    public ImageSharpProcessor(ILogger<ImageSharpProcessor> logger) => _logger = logger;

    public async Task<Result<ProcessedImage>> ProcessAsync(
        Stream input, ImageKind kind, CancellationToken ct = default)
    {
        // Identify and Load both need to read from the start, and a request body cannot seek.
        await using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);

        if (buffer.Length == 0) return Result<ProcessedImage>.Fail(ErrorCodes.ImageMissing);

        await Gate.WaitAsync(ct);

        try
        {
            buffer.Position = 0;
            var info = await Image.IdentifyAsync(Decoder, buffer, ct);

            if ((long)info.Width * info.Height > MaxPixels)
                return Result<ProcessedImage>.Fail(ErrorCodes.ImageInvalid);

            buffer.Position = 0;
            using var image = await Image.LoadAsync(Decoder, buffer, ct);

            image.Mutate(x =>
            {
                // Phones store photos sideways plus a "rotate me" EXIF flag. Apply it now,
                // because the metadata carrying it is about to be dropped.
                x.AutoOrient();

                x.Resize(kind == ImageKind.Avatar
                    ? new ResizeOptions
                    {
                        Size = new Size(AvatarSize, AvatarSize),
                        Mode = ResizeMode.Crop
                    }
                    : new ResizeOptions
                    {
                        Size = new Size(CoverMaxSide, CoverMaxSide),
                        Mode = ResizeMode.Max
                    });
            });

            // GPS position, camera serial, original timestamps — none of it is published.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            await using var output = new MemoryStream();
            await image.SaveAsync(output, Encoder, ct);

            return Result<ProcessedImage>.Ok(new ProcessedImage(output.ToArray(), "image/webp", "webp"));
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException)
        {
            // Unknown format, corrupt data, or a format this decoder set does not include.
            _logger.LogInformation("Rejected an upload that is not a supported image: {Reason}", ex.Message);
            return Result<ProcessedImage>.Fail(ErrorCodes.ImageInvalid);
        }
        finally
        {
            Gate.Release();
        }
    }
}
