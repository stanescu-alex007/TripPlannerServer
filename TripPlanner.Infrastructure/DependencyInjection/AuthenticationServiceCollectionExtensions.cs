using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TripPlanner.Core.Common;

namespace TripPlanner.Infrastructure.DependencyInjection
{
    public static class AuthenticationServiceCollectionExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
            this IServiceCollection services, AuthSettings settings)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = settings.JwtIssuer,
                    ValidAudience = settings.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtKey)),

                    // Default is five minutes of leeway, which makes a 15-minute token
                    // effectively 20. Tokens expire when they say they do.
                    ClockSkew = TimeSpan.Zero
                };

                // Emit the same {code, message} body the rest of the API uses, instead of
                // the framework's empty 401/403.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await WriteError(context.Response, ErrorCodes.Unauthenticated);
                    },
                    OnForbidden = context => WriteError(context.Response, ErrorCodes.Forbidden)
                };
            });

            return services;
        }

        private static async Task WriteError(HttpResponse response, Error error)
        {
            if (response.HasStarted) return;

            response.StatusCode = error.Type == ErrorType.Forbidden ? 403 : 401;
            response.ContentType = "application/json";

            await response.WriteAsJsonAsync(new { code = error.Code, message = error.Message });
        }
    }
}
