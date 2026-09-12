using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TripPlanner.Core.Common;

namespace TripPlanner.WEB;

public static class RateLimitPolicies
{
    /// <summary>Sign-in attempts — the main brute-force target.</summary>
    public const string Login = "auth-login";

    /// <summary>Account creation, to blunt automated sign-up floods.</summary>
    public const string Registration = "auth-registration";

    /// <summary>
    /// Anything that sends an email or redeems a one-time token. Limits both mail
    /// spamming and token guessing.
    /// </summary>
    public const string Sensitive = "auth-sensitive";
}

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Same body shape as every other error, so the client can handle it uniformly.
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    code = ErrorCodes.TooManyAttempts.Code,
                    message = ErrorCodes.TooManyAttempts.Message
                }, ct);
            };

            options.AddPolicy(RateLimitPolicies.Login, PartitionByIp(permit: 10, perMinutes: 5));
            options.AddPolicy(RateLimitPolicies.Registration, PartitionByIp(permit: 5, perMinutes: 60));
            options.AddPolicy(RateLimitPolicies.Sensitive, PartitionByIp(permit: 10, perMinutes: 15));
        });

        return services;
    }

    /// <summary>
    /// A fixed window keyed on the caller's IP.
    /// <para>
    /// Note for deployment: behind a reverse proxy every request appears to come from
    /// the proxy unless <c>UseForwardedHeaders</c> is configured, which would collapse
    /// all callers into one partition.
    /// </para>
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> PartitionByIp(int permit, int perMinutes) =>
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permit,
                Window = TimeSpan.FromMinutes(perMinutes),
                QueueLimit = 0
            });
}
