using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services.Auth
{
    public class RegistrationService : IRegistrationService
    {
        private readonly IUserRepository _users;
        private readonly IPasswordService _passwords;
        private readonly IOneTimeTokenService _oneTimeTokens;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _email;

        /// <summary>
        /// Whatever else in the system wants to know an account was verified. Registration
        /// announces the fact and stays ignorant of who listens.
        /// </summary>
        private readonly IEnumerable<IUserVerifiedHandler> _verifiedHandlers;

        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(
            IUserRepository users,
            IPasswordService passwords,
            IOneTimeTokenService oneTimeTokens,
            IUnitOfWork unitOfWork,
            IEmailSender email,
            IEnumerable<IUserVerifiedHandler> verifiedHandlers,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<RegistrationService> logger)
        {
            _users = users;
            _passwords = passwords;
            _oneTimeTokens = oneTimeTokens;
            _unitOfWork = unitOfWork;
            _email = email;
            _verifiedHandlers = verifiedHandlers;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        public async Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var email = EmailAddress.Normalize(request.Email);

            var policyError = PasswordPolicy.Validate(
                request.Password, _settings, email, request.FirstName, request.LastName);

            if (policyError is not null) return Result.Fail(policyError);

            var existing = await _users.GetByEmailAsync(email, ct);

            if (existing is not null)
            {
                // An account that was never confirmed is not really taken. Letting the
                // address stay claimed forever means one typo at registration locks the
                // real owner out permanently, with no way to recover it.
                if (existing.IsEmailVerified)
                    return Result.Fail(ErrorCodes.EmailAlreadyRegistered);

                // The pending account is left exactly as it is — above all its password.
                // Overwriting it was an account takeover: anyone could re-register a
                // victim's pending address with a password of their own, and the moment
                // the victim clicked the (genuine) verification link, the attacker's
                // password would open the verified account. The address still is not
                // "taken": the real owner gets a fresh link here, and once verified can
                // set a password of their choosing through forgot-password.
                //
                // Hashed and thrown away so this branch costs what a real registration
                // costs: skipping Argon2 would make the response measurably faster and
                // reveal the pending account through timing.
                _ = _passwords.Hash(request.Password);

                await SendVerificationEmailAsync(existing, ct);

                _logger.LogInformation(
                    "Registration for the unconfirmed account {UserId}; verification link resent.", existing.Id);

                // The same response as a brand-new registration, so this endpoint does not
                // reveal that the address already had a pending account.
                return Result.Ok();
            }

            var user = new User
            {
                Email = email,
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                BirthDate = ToUtcDate(request.BirthDate),
                PasswordHash = _passwords.Hash(request.Password),
                EmailVerifiedAt = null,
                TermsAcceptedAt = _clock.GetUtcNow().UtcDateTime
            };

            try
            {
                await _users.AddAsync(user, ct);
            }
            catch (DuplicateEmailException)
            {
                // Lost a race with a concurrent registration; the unique index decided.
                return Result.Fail(ErrorCodes.EmailAlreadyRegistered);
            }

            await SendVerificationEmailAsync(user, ct);
            return Result.Ok();
        }

        public async Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct = default)
        {
            var found = await _oneTimeTokens.FindUsableAsync(rawToken, UserTokenPurpose.EmailVerification, ct);
            if (!found.Success) return Result.Fail(found.Error!);

            var token = found.Data!;
            var user = token.User;

            if (user.IsEmailVerified)
                return Result.Fail(ErrorCodes.EmailAlreadyVerified);

            // Together, so a failed write cannot burn the link without verifying anyone.
            var consumed = await _unitOfWork.ExecuteAsync(async t =>
            {
                if (!await _oneTimeTokens.TryConsumeAsync(token, t)) return false;

                user.EmailVerifiedAt = _clock.GetUtcNow().UtcDateTime;
                await _users.UpdateAsync(user, t);
                return true;
            }, ct);

            if (!consumed) return Result.Fail(ErrorCodes.TokenAlreadyUsed);

            // Outside the transaction on purpose. Handlers swallow their own failures,
            // so a follow-up action cannot undo a verification that already succeeded.
            foreach (var handler in _verifiedHandlers)
                await handler.HandleAsync(user.Id, user.Email, ct);

            _logger.LogInformation("Email verified for user {UserId}.", user.Id);
            return Result.Ok();
        }

        public async Task<Result> ResendVerificationAsync(string email, CancellationToken ct = default)
        {
            var user = await _users.GetByEmailAsync(EmailAddress.Normalize(email), ct);

            // Still 200 inside the cooldown, so the response reveals nothing about the account.
            if (user is not null && !user.IsEmailVerified
                && !await _oneTimeTokens.IssuedRecentlyAsync(
                    user.Id, UserTokenPurpose.EmailVerification, AuthSettings.EmailResendCooldown, ct))
            {
                await SendVerificationEmailAsync(user, ct);
            }

            return Result.Ok();
        }

        private async Task SendVerificationEmailAsync(User user, CancellationToken ct)
        {
            var raw = await _oneTimeTokens.IssueAsync(
                user.Id, UserTokenPurpose.EmailVerification,
                TimeSpan.FromHours(_settings.EmailVerificationHours), ct: ct);

            await _email.TrySendAsync(
                AuthEmailTemplates.VerifyEmail(
                    user, FrontendLinks.VerifyEmail(_settings, raw), _settings.EmailVerificationHours),
                _logger, ct);
        }

        private static DateTime? ToUtcDate(DateTime? value) =>
            value is null ? null : DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc);
    }
}
