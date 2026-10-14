using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Everything a signed-in user does to secure their own account: two-factor,
    /// recovery codes, changing their email, and managing where they are signed in.
    /// Kept apart from <see cref="ISignInService"/>, which is only about getting in.
    /// </summary>
    public interface IAccountSecurityService
    {
        // ── Two-factor ────────────────────────────────────────────────────────

        /// <summary>
        /// Produces a secret and QR URI. Nothing is stored until the user proves they
        /// can generate a valid code, so an abandoned setup cannot lock anyone out.
        /// </summary>
        Task<Result<TwoFactorSetupResponse>> BeginTwoFactorSetupAsync(
            Guid userId, CancellationToken ct = default);

        /// <summary>Confirms the secret with a live code and switches 2FA on.</summary>
        Task<Result<RecoveryCodesResponse>> EnableTwoFactorAsync(
            Guid userId, string secret, string code, CancellationToken ct = default);

        /// <summary>Requires the password — otherwise a stolen session could just turn it off.</summary>
        Task<Result> DisableTwoFactorAsync(Guid userId, string password, CancellationToken ct = default);

        /// <summary>Issues a fresh set, invalidating every previous code.</summary>
        Task<Result<RecoveryCodesResponse>> RegenerateRecoveryCodesAsync(
            Guid userId, string password, CancellationToken ct = default);

        // ── Email change ──────────────────────────────────────────────────────

        /// <summary>
        /// Starts a change: confirms ownership of the new address by email before
        /// anything moves, and warns the old address that it is happening.
        /// </summary>
        Task<Result> RequestEmailChangeAsync(
            Guid userId, string newEmail, string password, CancellationToken ct = default);

        Task<Result> ConfirmEmailChangeAsync(string rawToken, CancellationToken ct = default);

        // ── Sessions ──────────────────────────────────────────────────────────

        /// <summary>
        /// Live sessions, newest first, with the caller's own flagged.
        /// <paramref name="currentSessionId"/> comes from the access token's "sid" claim —
        /// the refresh cookie is scoped to the auth endpoints and never arrives here.
        /// </summary>
        Task<Result<List<SessionDto>>> GetSessionsAsync(
            Guid userId, Guid? currentSessionId, CancellationToken ct = default);

        Task<Result> RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);

        /// <summary>Signs out everywhere except the caller's own session.</summary>
        Task<Result<int>> RevokeOtherSessionsAsync(
            Guid userId, Guid? currentSessionId, CancellationToken ct = default);
    }
}
