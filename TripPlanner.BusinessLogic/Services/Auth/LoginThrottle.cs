using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class LoginThrottle : ILoginThrottle
    {
        private readonly IUserRepository _users;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;

        public LoginThrottle(IUserRepository users, AuthSettings settings, TimeProvider clock)
        {
            _users = users;
            _settings = settings;
            _clock = clock;
        }

        /// <summary>
        /// Doubles per failure past the threshold, capped — a delay rather than a lockout,
        /// so an attacker cannot deliberately shut a user out of their own account.
        /// </summary>
        public bool IsThrottled(User user)
        {
            if (user.FailedLoginAttempts < _settings.LoginFailuresBeforeDelay) return false;
            if (user.LastFailedLoginAt is null) return false;

            var over = user.FailedLoginAttempts - _settings.LoginFailuresBeforeDelay;

            // Exponent capped before raising, so the multiplier cannot overflow.
            var multiplier = Math.Pow(2, Math.Min(over, 16));
            var seconds = Math.Min(_settings.LoginDelayBaseSeconds * multiplier, _settings.LoginDelayMaxSeconds);

            var readyAt = user.LastFailedLoginAt.Value.AddSeconds(seconds);

            return readyAt > _clock.GetUtcNow().UtcDateTime;
        }

        // Both writes go through targeted statements on the throttle columns alone. A
        // whole-entity save here wrote back every value loaded at the start of the
        // request, which could revert a TOTP step just spent, or a password or email
        // changed concurrently elsewhere.

        public Task RecordFailureAsync(User user, CancellationToken ct = default) =>
            _users.RecordFailedLoginAsync(user, ct);

        public async Task ResetAsync(User user, CancellationToken ct = default)
        {
            if (user.FailedLoginAttempts == 0 && user.LastFailedLoginAt is null) return;

            await _users.ResetFailedLoginsAsync(user, ct);
        }
    }
}
