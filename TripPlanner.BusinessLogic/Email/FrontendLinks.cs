using TripPlanner.Core.Common;

namespace TripPlanner.BusinessLogic.Email
{
    /// <summary>
    /// Links placed in emails. The paths are routes in the Angular app, so they are
    /// listed together here rather than spread across the services that send them.
    /// </summary>
    internal static class FrontendLinks
    {
        public static string VerifyEmail(AuthSettings settings, string token) =>
            Build(settings, "auth/verify-email", token);

        public static string ResetPassword(AuthSettings settings, string token) =>
            Build(settings, "auth/reset-password", token);

        public static string ConfirmEmailChange(AuthSettings settings, string token) =>
            Build(settings, "auth/confirm-email-change", token);

        public static string TripInvitation(AuthSettings settings, string token) =>
            Build(settings, "trips/invitation", token);

        public static string TripDetail(AuthSettings settings, Guid tripId) =>
            $"{settings.FrontendBaseUrl.TrimEnd('/')}/trips/detail/{tripId}";

        // Tokens come from ISecureTokenService and are already URL-safe.
        private static string Build(AuthSettings settings, string path, string token) =>
            $"{settings.FrontendBaseUrl.TrimEnd('/')}/{path}?token={token}";
    }
}
