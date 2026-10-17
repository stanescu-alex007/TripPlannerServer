using System.Text.RegularExpressions;

namespace TripPlanner.Infrastructure.Images;

/// <summary>
/// Object names and the URLs they map to, shared by every storage provider so a URL
/// issued by one is understood the same way on delete.
/// </summary>
internal static partial class ImageKeys
{
    /// <summary>"avatars/3f9c…e1.webp" — a folder we know, 32 hex characters, an extension.</summary>
    [GeneratedRegex(@"^(avatars|trips)/[0-9a-f]{32}\.[a-z]{3,4}$")]
    private static partial Regex KeyPattern();

    /// <summary>A random name, so URLs cannot be guessed and are never reused.</summary>
    public static string NewKey(string folder, string extension) =>
        $"{folder}/{Guid.NewGuid():N}.{extension}";

    public static string ToUrl(string publicBaseUrl, string key) =>
        $"{publicBaseUrl.TrimEnd('/')}/{key}";

    /// <summary>
    /// The key behind a URL we issued, or null for anything else — a link pasted before
    /// uploads existed, another host, or a crafted value such as "../" aiming outside
    /// the folder. Only keys matching the exact shape we generate are ever acted on.
    /// </summary>
    public static string? FromUrl(string publicBaseUrl, string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var prefix = publicBaseUrl.TrimEnd('/') + "/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return null;

        var key = url[prefix.Length..];
        return KeyPattern().IsMatch(key) ? key : null;
    }
}
