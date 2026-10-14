using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace TripPlanner.WEB.DependencyInjection;

/// <summary>
/// Decides whose <c>X-Forwarded-For</c> is believed.
///
/// <para>
/// Behind a reverse proxy every request otherwise appears to come from the proxy, so
/// the rate limiter puts all callers in one bucket and every session is recorded
/// against the proxy's address. The header fixes that — but only if it cannot be set
/// by the caller. Trusting it from anyone means anyone can invent a new address per
/// request and walk straight past the login, registration and reset limits.
/// </para>
///
/// <para>
/// So the proxies are named in configuration, and with none named the framework's own
/// default stands: loopback only, which for a directly exposed API means the header is
/// ignored entirely.
/// </para>
/// </summary>
public static class ForwardedHeadersServiceCollectionExtensions
{
    public static IServiceCollection AddTrustedProxies(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("ForwardedHeaders");

        // Parsed up front: a typo in a proxy address has to stop the application here,
        // not silently leave the header untrusted (or over-trusted) at runtime.
        var proxies = (section.GetSection("KnownProxies").Get<string[]>() ?? [])
            .Select(ParseAddress).ToList();

        var networks = (section.GetSection("KnownNetworks").Get<string[]>() ?? [])
            .Select(ParseNetwork).ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (proxies.Count == 0 && networks.Count == 0) return;

            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
            foreach (var network in networks) options.KnownNetworks.Add(network);
        });

        return services;
    }

    private static IPAddress ParseAddress(string value) =>
        IPAddress.TryParse(value, out var address)
            ? address
            : throw new InvalidOperationException(
                $"ForwardedHeaders:KnownProxies contains '{value}', which is not an IP address.");

    /// <summary>Accepts CIDR notation, e.g. "10.0.0.0/8".</summary>
    private static IPNetwork ParseNetwork(string value)
    {
        var parts = value.Split('/');

        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], out var length))
            return new IPNetwork(prefix, length);

        throw new InvalidOperationException(
            $"ForwardedHeaders:KnownNetworks contains '{value}', which is not a CIDR range like 10.0.0.0/8.");
    }
}
