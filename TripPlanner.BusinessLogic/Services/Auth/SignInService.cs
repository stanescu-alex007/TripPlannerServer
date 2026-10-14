using Microsoft.Extensions.Logging;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class SignInService : ISignInService
    {
        private readonly IUserRepository _users;
        private readonly IPasswordService _passwords;
        private readonly ILoginThrottle _throttle;
        private readonly IOneTimeTokenService _oneTimeTokens;
        private readonly ITwoFactorCredentialService _twoFactor;
        private readonly ISessionIssuer _sessions;
        private readonly AuthSettings _settings;
        private readonly ILogger<SignInService> _logger;

        public SignInService(
            IUserRepository users,
            IPasswordService passwords,
            ILoginThrottle throttle,
            IOneTimeTokenService oneTimeTokens,
            ITwoFactorCredentialService twoFactor,
            ISessionIssuer sessions,
            AuthSettings settings,
            ILogger<SignInService> logger)
        {
            _users = users;
            _passwords = passwords;
            _throttle = throttle;
            _oneTimeTokens = oneTimeTokens;
            _twoFactor = twoFactor;
            _sessions = sessions;
            _settings = settings;
            _logger = logger;
        }

        public async Task<Result<LoginOutcome>> LoginAsync(
            LoginRequest request, RequestContext context, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(EmailAddress.Normalize(request.Email), ct);

            // Checked before the password, so a throttled attempt learns nothing.
            if (user is not null && _throttle.IsThrottled(user))
            {
                _logger.LogWarning("Throttled sign-in attempt for user {UserId}.", user.Id);
                return Result<LoginOutcome>.Fail(ErrorCodes.LoginThrottled);
            }

            // Hash even when the account is missing or passwordless, so the response
            // time does not reveal which addresses exist.
            var passwordValid = user?.PasswordHash is not null
                ? _passwords.Verify(request.Password, user.PasswordHash)
                : _passwords.VerifyDummy(request.Password);

            if (user is null || !passwordValid)
            {
                if (user is not null) await _throttle.RecordFailureAsync(user, ct);
                return Result<LoginOutcome>.Fail(ErrorCodes.InvalidCredentials);
            }

            if (!user.IsEmailVerified)
                return Result<LoginOutcome>.Fail(ErrorCodes.EmailNotVerified);

            if (user.IsTwoFactorEnabled)
            {
                // The failure count is deliberately NOT reset here. The counter is shared
                // with the second step, so clearing it on a correct password would let
                // anyone who knows the password wipe the slate between batches of code
                // guesses — re-entering the password every few attempts would keep the
                // six digits permanently unthrottled. It is reset only once the second
                // factor succeeds, in CompleteTwoFactorAsync.
                var challenge = await _oneTimeTokens.IssueAsync(
                    user.Id, UserTokenPurpose.TwoFactorChallenge,
                    TimeSpan.FromMinutes(_settings.TwoFactorChallengeMinutes), ct: ct);

                return Result<LoginOutcome>.Ok(LoginOutcome.NeedsTwoFactor(challenge));
            }

            // No second factor: the password was the whole proof, so the slate is clean.
            await _throttle.ResetAsync(user, ct);

            var tokens = await _sessions.StartAsync(user, context, ct);

            _logger.LogInformation("User {UserId} signed in.", user.Id);
            return Result<LoginOutcome>.Ok(LoginOutcome.SignedIn(tokens));
        }

        public async Task<Result<AuthTokens>> CompleteTwoFactorAsync(
            string challengeToken, string code, RequestContext context, CancellationToken ct = default)
        {
            var found = await _oneTimeTokens.FindUsableAsync(
                challengeToken, UserTokenPurpose.TwoFactorChallenge, ct);

            if (!found.Success)
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorChallengeInvalid);

            var challenge = found.Data!;
            var user = challenge.User;

            if (!user.IsTwoFactorEnabled)
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorNotEnabled);

            // Wrong codes were counted but never enforced here, so once the password was
            // known the six digits could be guessed at the full rate the IP limiter allows
            // for the life of each challenge.
            if (_throttle.IsThrottled(user))
            {
                _logger.LogWarning("Throttled two-factor attempt for user {UserId}.", user.Id);
                return Result<AuthTokens>.Fail(ErrorCodes.LoginThrottled);
            }

            if (!await _twoFactor.VerifyAsync(user, code, ct))
            {
                await _throttle.RecordFailureAsync(user, ct);
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorCodeInvalid);
            }

            // Burned only once the code was right, so a typo does not send the user back
            // to the password screen — and conditionally, so one challenge cannot be
            // redeemed into two sessions by parallel requests.
            if (!await _oneTimeTokens.TryConsumeAsync(challenge, ct))
                return Result<AuthTokens>.Fail(ErrorCodes.TwoFactorChallengeInvalid);

            await _throttle.ResetAsync(user, ct);

            var tokens = await _sessions.StartAsync(user, context, ct);

            _logger.LogInformation("User {UserId} completed two-factor sign-in.", user.Id);
            return Result<AuthTokens>.Ok(tokens);
        }
    }
}
