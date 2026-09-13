using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.BusinessLogic.Services;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.DependencyInjection
{
    public static class BusinessLogicServiceCollectionExtensions
    {
        public static IServiceCollection AddBusinessLogic(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(BuildAuthSettings(configuration));

            // Services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IAccountSecurityService, AccountSecurityService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITripService, TripService>();
            services.AddScoped<ITripScheduleService, TripScheduleService>();

            // Helpers
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<ISecureTokenService, SecureTokenService>();
            services.AddSingleton<ITwoFactorService, TwoFactorService>();
            services.AddScoped<IJwtService, JwtService>();

            return services;
        }

        /// <summary>
        /// Binds and validates auth configuration once at startup, so a missing signing
        /// key fails immediately with a clear message instead of surfacing as a
        /// NullReferenceException on the first login.
        /// </summary>
        public static AuthSettings BuildAuthSettings(IConfiguration configuration)
        {
            var jwt = configuration.GetSection("Jwt");
            var key = jwt["Key"];

            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "Jwt:Key is not configured. In development set it with: " +
                    "dotnet user-secrets set \"Jwt:Key\" \"<at least 32 random bytes, base64>\"");

            // HMAC-SHA256 needs a key at least as long as its 256-bit output.
            if (Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException(
                    "Jwt:Key is too short — HMAC-SHA256 requires at least 32 bytes.");

            return new AuthSettings
            {
                JwtKey = key,
                JwtIssuer = jwt["Issuer"] ?? "TripPlanner",
                JwtAudience = jwt["Audience"] ?? "TripPlanner",
                AccessTokenMinutes = ReadInt(jwt, "AccessTokenMinutes", 15),
                RefreshTokenDays = ReadInt(jwt, "RefreshTokenDays", 7),
                RefreshFamilyDays = ReadInt(jwt, "RefreshFamilyDays", 30),
                FrontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200"
            };
        }

        /// <summary>
        /// Reads an int without pulling in Configuration.Binder — this project only
        /// references Configuration.Abstractions.
        /// </summary>
        private static int ReadInt(IConfiguration section, string key, int fallback) =>
            int.TryParse(section[key], out var value) && value > 0 ? value : fallback;
    }
}
