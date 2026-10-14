using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TripPlanner.BusinessLogic.Services;
using TripPlanner.BusinessLogic.Services.Auth;
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

            services.AddSingleton(BuildSecretProtectionKeys(configuration));
            services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();

            // Authentication, one service per responsibility
            services.AddScoped<IRegistrationService, RegistrationService>();
            services.AddScoped<ISignInService, SignInService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IPasswordManagementService, PasswordManagementService>();

            // Building blocks the auth services share
            services.AddScoped<ISessionIssuer, SessionIssuer>();
            services.AddScoped<IOneTimeTokenService, OneTimeTokenService>();
            services.AddScoped<ILoginThrottle, LoginThrottle>();
            services.AddScoped<ITwoFactorCredentialService, TwoFactorCredentialService>();

            // Services
            services.AddScoped<IAccountSecurityService, AccountSecurityService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITripService, TripService>();
            services.AddScoped<ITripScheduleService, TripScheduleService>();
            services.AddScoped<ITripInvitationService, TripInvitationService>();
            services.AddScoped<ITripOwnershipService, TripOwnershipService>();

            // Contact page → team inbox. Left unset, the endpoint answers CONTACT_UNAVAILABLE
            // instead of failing start-up, so local development needs no extra setup.
            services.AddSingleton(new ContactSettings(configuration["Contact:InboxAddress"]));
            services.AddScoped<IContactService, ContactService>();

            // The trips module subscribes to account events, so auth does not have to
            // know it exists.
            services.AddScoped<IUserVerifiedHandler, LinkInvitationsOnVerifiedHandler>();
            services.AddScoped<IUserEmailChangedHandler, SyncParticipantsOnEmailChangedHandler>();

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
                RefreshReuseGraceSeconds = ReadInt(jwt, "RefreshReuseGraceSeconds", 30),
                FrontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:4200"
            };
        }

        /// <summary>
        /// Loads the key ring that encrypts secrets at rest. Refuses to start without one:
        /// running on a missing or malformed key would not fail loudly, it would write
        /// secrets nobody can read back.
        /// </summary>
        public static SecretProtectionKeys BuildSecretProtectionKeys(IConfiguration configuration)
        {
            const string Hint =
                "In development set it with: dotnet user-secrets set \"SecretProtection:Keys:k1\" " +
                "\"<32 random bytes, base64>\" (and SecretProtection:ActiveKeyId to \"k1\"). " +
                "Losing a key makes every secret written with it unreadable — back it up like the JWT key.";

            var section = configuration.GetSection("SecretProtection");
            var activeKeyId = section["ActiveKeyId"];

            if (string.IsNullOrWhiteSpace(activeKeyId))
                throw new InvalidOperationException("SecretProtection:ActiveKeyId is not configured. " + Hint);

            var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in section.GetSection("Keys").GetChildren())
            {
                // The id is embedded in the stored value between '.' separators.
                if (entry.Key.Length == 0 || !entry.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                    throw new InvalidOperationException(
                        $"SecretProtection key id '{entry.Key}' may only contain letters, digits, '-' and '_'.");

                byte[] material;
                try
                {
                    material = Convert.FromBase64String(entry.Value ?? string.Empty);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException($"SecretProtection:Keys:{entry.Key} is not valid base64.");
                }

                if (material.Length != 32)
                    throw new InvalidOperationException(
                        $"SecretProtection:Keys:{entry.Key} must decode to exactly 32 bytes for AES-256.");

                keys[entry.Key] = material;
            }

            if (!keys.ContainsKey(activeKeyId))
                throw new InvalidOperationException(
                    $"SecretProtection:Keys:{activeKeyId} (the active key) is not configured. " + Hint);

            return new SecretProtectionKeys(activeKeyId, keys);
        }

        /// <summary>
        /// Reads an int without pulling in Configuration.Binder — this project only
        /// references Configuration.Abstractions.
        /// </summary>
        private static int ReadInt(IConfiguration section, string key, int fallback) =>
            int.TryParse(section[key], out var value) && value > 0 ? value : fallback;
    }
}
