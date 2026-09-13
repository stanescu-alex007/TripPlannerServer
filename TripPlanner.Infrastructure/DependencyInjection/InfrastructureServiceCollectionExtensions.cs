using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;
using TripPlanner.Infrastructure.Data.Interceptors;
using TripPlanner.Infrastructure.Repositories;

namespace TripPlanner.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. " +
                "In development set it with: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"");

        // Injected rather than using DateTime.UtcNow directly, so time-dependent
        // behaviour (token expiry, audit stamps) is testable.
        services.TryAddSingletonTimeProvider();

        services.AddSingleton<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) =>
            options.UseNpgsql(connectionString)
                   .AddInterceptors(provider.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserTokenRepository, UserTokenRepository>();
        services.AddScoped<IUserRecoveryCodeRepository, UserRecoveryCodeRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<ITripScheduleRepository, TripScheduleRepository>();

        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
