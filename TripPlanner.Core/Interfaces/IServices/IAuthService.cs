using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Context about the caller, recorded on issued refresh tokens so a session can
    /// later be recognised in the "your devices" list.
    /// </summary>
    public sealed record RequestContext(string? IpAddress, string? UserAgent);

    /// <summary>
    /// What a password check produced: either a signed-in session, or a challenge
    /// that still needs a second factor.
    /// </summary>
    public sealed record LoginOutcome
    {
        public AuthTokens? Tokens { get; init; }

        /// <summary>Opaque, short-lived and single use. Useless without the code.</summary>
        public string? ChallengeToken { get; init; }

        public bool RequiresTwoFactor => ChallengeToken is not null;

        public static LoginOutcome SignedIn(AuthTokens tokens) => new() { Tokens = tokens };

        public static LoginOutcome NeedsTwoFactor(string challengeToken) =>
            new() { ChallengeToken = challengeToken };
    }

    public interface IAuthService
    {
        Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

        Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct = default);

        /// <summary>Always reports success, so it cannot be used to probe for accounts.</summary>
        Task<Result> ResendVerificationAsync(string email, CancellationToken ct = default);

        /// <summary>
        /// Verifies the password. Returns a challenge instead of tokens when the account
        /// has two-factor enabled.
        /// </summary>
        Task<Result<LoginOutcome>> LoginAsync(
            LoginRequest request, RequestContext context, CancellationToken ct = default);

        /// <summary>
        /// Completes a sign-in with an authenticator code or a recovery code, exchanging
        /// the challenge for real tokens.
        /// </summary>
        Task<Result<AuthTokens>> CompleteTwoFactorAsync(
            string challengeToken, string code, RequestContext context, CancellationToken ct = default);

        Task<Result<AuthTokens>> RefreshAsync(
            string rawRefreshToken, RequestContext context, CancellationToken ct = default);

        Task<Result> LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);

        Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default);

        Task<Result> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct = default);

        Task<Result<AuthTokens>> ChangePasswordAsync(
            Guid userId, string currentPassword, string newPassword,
            RequestContext context, CancellationToken ct = default);
    }
}
