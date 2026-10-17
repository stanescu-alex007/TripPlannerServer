using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IRepositories;

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

                options.Events = new JwtBearerEvents
                {
                    // A signature and an expiry are not enough: the session behind the
                    // token can have ended in the meantime — the password was changed,
                    // the device was signed out, the account was deleted. Without this
                    // check an access token stayed usable for the rest of its 15 minutes
                    // after all of those.
                    OnTokenValidated = EnsureSessionIsLive,

                    // Emit the same {code, message} body the rest of the API uses, instead
                    // of the framework's empty 401/403.
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

        /// <summary>
        /// Costs one indexed lookup per authenticated request — the price of being able
        /// to end a session immediately rather than waiting out the token.
        /// </summary>
        private static async Task EnsureSessionIsLive(TokenValidatedContext context)
        {
            var principal = context.Principal;

            var hasUser = Guid.TryParse(
                principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId);

            // "sid" is the refresh-token family the access token was issued with.
            var hasSession = Guid.TryParse(principal?.FindFirst("sid")?.Value, out var sessionId);

            if (!hasUser || !hasSession)
            {
                context.Fail("The token does not identify a session.");
                return;
            }

            var sessions = context.HttpContext.RequestServices
                .GetRequiredService<IRefreshTokenRepository>();

            if (!await sessions.IsSessionLiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
                context.Fail("The session behind this token has ended.");
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
