using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Infrastructure.Images;

namespace TripPlanner.Infrastructure.DependencyInjection;

public static class ImageServiceCollectionExtensions
{
    public static IServiceCollection AddImageStorage(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ImageOptions.SectionName);
        services.Configure<ImageOptions>(section);

        var options = section.Get<ImageOptions>() ?? new ImageOptions();

        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();

        if (!string.Equals(options.Provider, "R2", StringComparison.OrdinalIgnoreCase))
        {
            // Development default: a local folder the API serves under /uploads.
            services.AddSingleton<LocalImageStorage>();
            services.AddSingleton<IImageStorage>(sp => sp.GetRequiredService<LocalImageStorage>());
            return services;
        }

        var r2 = options.R2;

        if (string.IsNullOrWhiteSpace(r2.ServiceUrl) || string.IsNullOrWhiteSpace(r2.Bucket)
            || string.IsNullOrWhiteSpace(r2.AccessKeyId) || string.IsNullOrWhiteSpace(r2.SecretAccessKey))
            throw new InvalidOperationException(
                "Images:Provider is 'R2' but Images:R2 (ServiceUrl, Bucket, AccessKeyId, " +
                "SecretAccessKey) is incomplete.");

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(r2.AccessKeyId, r2.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = r2.ServiceUrl,
                ForcePathStyle = true,

                // R2 rejects the checksums newer SDKs attach to every request by default.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            }));

        services.AddSingleton<IImageStorage, R2ImageStorage>();

        return services;
    }
}
