using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
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

    /// <summary>Getting in: the password step and, when enabled, the second factor.</summary>
    public interface ISignInService
    {
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
    }
}
