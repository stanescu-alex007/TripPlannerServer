using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Infrastructure.Data;
using TripPlanner.Infrastructure.Repositories;

namespace TripPlanner.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<ITripScheduleRepository, TripScheduleRepository>();

        return services;
    }
}