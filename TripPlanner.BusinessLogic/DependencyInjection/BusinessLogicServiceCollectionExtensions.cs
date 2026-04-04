using Microsoft.Extensions.DependencyInjection;
using TripPlanner.BusinessLogic.Services;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.DependencyInjection 
{
    public static class BusinessLogicServiceCollectionExtensions
    {
        public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
        {
            // Services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();

            // Helpers
            services.AddScoped<IPasswordService, PasswordService>();
            services.AddScoped<IJwtService, JwtService>();

            return services;
        }
    }

}
