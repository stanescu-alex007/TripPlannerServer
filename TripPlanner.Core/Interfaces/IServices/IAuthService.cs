using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Context about the caller, recorded on issued refresh tokens so a suspicious
    /// session can later be traced.
    /// </summary>
    public sealed record RequestContext(string? IpAddress, string? UserAgent);

    public interface IAuthService
    {
        /// <summary>Creates an unverified account and emails a verification link.</summary>
        Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

        /// <summary>Redeems an email-verification token.</summary>
        Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct = default);

        /// <summary>
        /// Issues a fresh verification link. Always reports success, so it cannot be used
        /// to probe which addresses are registered.
        /// </summary>
        Task<Result> ResendVerificationAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Signs in and starts a new refresh-token family. Fails with
        /// <c>AUTH_EMAIL_NOT_VERIFIED</c> when the account exists but is unconfirmed.
        /// </summary>
        Task<Result<AuthTokens>> LoginAsync(LoginRequest request, RequestContext context, CancellationToken ct = default);

        /// <summary>
        /// Rotates a refresh token. Replaying an already-rotated token detonates the
        /// whole family and fails with <c>AUTH_REFRESH_TOKEN_REUSED</c>.
        /// </summary>
        Task<Result<AuthTokens>> RefreshAsync(string rawRefreshToken, RequestContext context, CancellationToken ct = default);

        /// <summary>Revokes the family the supplied token belongs to.</summary>
        Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);

        /// <summary>
        /// Emails a reset link. Always reports success regardless of whether the address
        /// exists, so the endpoint cannot enumerate accounts.
        /// </summary>
        Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default);

        /// <summary>Redeems a reset token, sets the new password and ends every session.</summary>
        Task<Result> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct = default);

        /// <summary>
        /// Changes the password of a signed-in user, ends every other session and
        /// returns a fresh pair for the caller.
        /// </summary>
        Task<Result<AuthTokens>> ChangePasswordAsync(
            Guid userId, string currentPassword, string newPassword, RequestContext context, CancellationToken ct = default);
    }
}
