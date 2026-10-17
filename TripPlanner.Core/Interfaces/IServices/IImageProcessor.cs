using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>What an image is for, which decides its size and shape.</summary>
    public enum ImageKind
    {
        /// <summary>Square, cropped to the centre.</summary>
        Avatar,

        /// <summary>Wide, kept at its own aspect ratio.</summary>
        TripCover
    }

    /// <summary>
    /// Turns an uploaded file into something safe to publish. Nothing a user uploads is
    /// stored as sent: the file is decoded (so a renamed script is rejected), rotated
    /// upright, shrunk, and re-encoded — which also drops EXIF, including the GPS
    /// position phones write into every photo.
    /// </summary>
    public interface IImageProcessor
    {
        Task<Result<ProcessedImage>> ProcessAsync(Stream input, ImageKind kind, CancellationToken ct = default);
    }
}
