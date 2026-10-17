using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.Infrastructure.Images;

/// <summary>
/// Development storage: writes images to a folder that the API serves under /uploads,
/// so uploads work locally without a bucket or credentials.
/// </summary>
public sealed class LocalImageStorage : IImageStorage
{
    private readonly ImageOptions _options;
    private readonly ILogger<LocalImageStorage> _logger;

    public LocalImageStorage(IOptions<ImageOptions> options, ILogger<LocalImageStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string RootDirectory => Path.GetFullPath(_options.Local.Directory);

    public async Task<string> SaveAsync(string folder, ProcessedImage image, CancellationToken ct = default)
    {
        var key = ImageKeys.NewKey(folder, image.Extension);
        var path = Path.Combine(RootDirectory, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, image.Data, ct);

        return ImageKeys.ToUrl(_options.PublicBaseUrl, key);
    }

    public Task DeleteAsync(string? url, CancellationToken ct = default)
    {
        var key = ImageKeys.FromUrl(_options.PublicBaseUrl, url);
        if (key is null) return Task.CompletedTask;

        try
        {
            File.Delete(Path.Combine(RootDirectory, key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image {Key}.", key);
        }

        return Task.CompletedTask;
    }
}
