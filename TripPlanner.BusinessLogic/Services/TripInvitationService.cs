using System.Globalization;
using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Entities;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    public class TripInvitationService : ITripInvitationService
    {
        /// <summary>Long enough to survive a holiday, short enough not to linger forever.</summary>
        private const int InvitationValidDays = 14;

        /// <summary>Stops the resend button from being used to spam an inbox.</summary>
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

        private readonly ITripInvitationRepository _invitations;
        private readonly ITripRepository _trips;
        private readonly IUserRepository _users;
        private readonly ISecureTokenService _secureTokens;
        private readonly IEmailSender _email;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _clock;
        private readonly ILogger<TripInvitationService> _logger;

        public TripInvitationService(
            ITripInvitationRepository invitations,
            ITripRepository trips,
            IUserRepository users,
            ISecureTokenService secureTokens,
            IEmailSender email,
            AuthSettings settings,
            TimeProvider clock,
            ILogger<TripInvitationService> logger)
        {
            _invitations = invitations;
            _trips = trips;
            _users = users;
            _secureTokens = secureTokens;
            _email = email;
            _settings = settings;
            _clock = clock;
            _logger = logger;
        }

        private DateTime Now => _clock.GetUtcNow().UtcDateTime;

        // ── SENDING ───────────────────────────────────────────────────────────

        public async Task<Result> SendAsync(Guid participantId, CancellationToken ct = default)
        {
            var participant = await _trips.GetParticipantWithTripAsync(participantId, ct);

            if (participant is null) return Result.Fail(ErrorCodes.ParticipantNotFound);

            // The owner is on the trip by definition; there is nothing to invite them to.
            if (string.Equals(participant.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase))
                return Result.Ok();

            // Already answered — re-sending would ask a question they settled.
            if (!string.Equals(participant.Status, TripParticipantStatus.NotConfirmed,
                    StringComparison.OrdinalIgnoreCase))
                return Result.Ok();

            await IssueAndSendAsync(participant, ct);
            return Result.Ok();
        }

        public async Task<int> SendPendingForTripAsync(Guid tripId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);
            if (trip is null) return 0;

            var sent = 0;

            foreach (var participant in trip.Participants)
            {
                if (string.Equals(participant.Role, TripParticipantRole.Owner, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.Equals(participant.Status, TripParticipantStatus.NotConfirmed,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip anyone who already has a live link, so editing a trip does not
                // re-mail everybody who simply has not replied yet.
                if (await _invitations.GetOpenForParticipantAsync(participant.Id, ct) is not null)
                    continue;

                participant.Trip = trip;
                await IssueAndSendAsync(participant, ct);
                sent++;
            }

            if (sent > 0)
                _logger.LogInformation("Sent {Count} invitation(s) for trip {TripId}.", sent, tripId);

            return sent;
        }

        public async Task<Result> ResendAsync(
            Guid tripId, Guid participantId, Guid requesterId, CancellationToken ct = default)
        {
            var trip = await _trips.GetByIdWithDetailsAsync(tripId, ct);

            if (trip is null) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.CanView(trip, requesterId)) return Result.Fail(ErrorCodes.TripNotFound);
            if (!TripAccess.IsOwner(trip, requesterId)) return Result.Fail(ErrorCodes.TripForbidden);

            var participant = trip.Participants.FirstOrDefault(p => p.Id == participantId);
            if (participant is null) return Result.Fail(ErrorCodes.ParticipantNotFound);

            if (!string.Equals(participant.Status, TripParticipantStatus.NotConfirmed,
                    StringComparison.OrdinalIgnoreCase))
                return Result.Fail(ErrorCodes.InvitationAlreadyAnswered);

            var existing = await _invitations.GetOpenForParticipantAsync(participant.Id, ct);

            if (existing is not null && Now - existing.SentAt < ResendCooldown)
                return Result.Fail(ErrorCodes.InvitationSendTooSoon);

            participant.Trip = trip;
            await IssueAndSendAsync(participant, ct);

            return Result.Ok();
        }

        // ── PREVIEW ───────────────────────────────────────────────────────────

        public async Task<Result<InvitationPreviewDto>> PreviewAsync(
            string rawToken, CancellationToken ct = default)
        {
            var (invitation, error) = await LoadAsync(rawToken, ct);

            // An answered or expired invitation still renders — the page has to explain
            // what happened instead of showing a bare "not found".
            if (invitation is null) return Result<InvitationPreviewDto>.Fail(error!);

            var participant = invitation.Participant;
            var trip = participant.Trip;

            var hasAccount = await _users.GetByEmailAsync(participant.Email, ct) is not null;

            return Result<InvitationPreviewDto>.Ok(new InvitationPreviewDto
            {
                TripId = trip.Id,
                TripName = trip.Name,
                Location = trip.Location,
                StartDate = FormatDate(trip.StartDate),
                EndDate = FormatDate(trip.EndDate),
                InvitedBy = trip.Owner?.FirstName ?? "The organiser",
                InvitedEmail = participant.Email,
                Role = participant.Role,
                IsOpen = invitation.IsOpen,
                Status = invitation.RespondedAt is null ? null : participant.Status,
                RequiresRegistration = !hasAccount
            });
        }

        // ── RESPONDING ────────────────────────────────────────────────────────

        public async Task<Result> AcceptByTokenAsync(
            string rawToken, Guid userId, CancellationToken ct = default)
        {
            var (invitation, error) = await LoadAsync(rawToken, ct);
            if (invitation is null) return Result.Fail(error!);

            if (!invitation.IsOpen) return Result.Fail(ClosedReason(invitation));

            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            // The invitation names an address. Accepting from a different account would
            // quietly put the wrong person on the trip.
            if (!string.Equals(user.Email, invitation.Participant.Email, StringComparison.OrdinalIgnoreCase))
                return Result.Fail(ErrorCodes.InvitationEmailMismatch);

            await ApplyResponseAsync(invitation, user, accepted: true, ct);
            return Result.Ok();
        }

        public async Task<Result> DeclineByTokenAsync(string rawToken, CancellationToken ct = default)
        {
            var (invitation, error) = await LoadAsync(rawToken, ct);
            if (invitation is null) return Result.Fail(error!);

            if (!invitation.IsOpen) return Result.Fail(ClosedReason(invitation));

            // No account required: forcing someone to register in order to say "no"
            // would mean most invitations are simply never answered.
            await ApplyResponseAsync(invitation, user: null, accepted: false, ct);
            return Result.Ok();
        }

        public async Task<Result> RespondInAppAsync(
            Guid tripId, Guid userId, bool accept, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            var participant = await _trips.GetParticipantByEmailAsync(tripId, user.Email, ct);
            if (participant is null) return Result.Fail(ErrorCodes.InvitationNotFound);

            if (!string.Equals(participant.Status, TripParticipantStatus.NotConfirmed,
                    StringComparison.OrdinalIgnoreCase))
                return Result.Fail(ErrorCodes.InvitationAlreadyAnswered);

            var invitation = await _invitations.GetOpenForParticipantAsync(participant.Id, ct);

            // No live link, but the seat is still pending — answer the seat itself.
            // The user is signed in as the invited address, which is proof enough.
            if (invitation is null)
            {
                await ApplyToParticipantAsync(participant, user, accept, ct);
                return Result.Ok();
            }

            invitation.Participant = participant;
            await ApplyResponseAsync(invitation, accept ? user : null, accept, ct);
            return Result.Ok();
        }

        // ── LISTING / LINKING ─────────────────────────────────────────────────

        public async Task<List<PendingInvitationDto>> GetPendingForUserAsync(
            Guid userId, CancellationToken ct = default)
        {
            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return [];

            var invitations = await _invitations.GetOpenForEmailAsync(user.Email, ct);

            return invitations
                .Where(i => string.Equals(i.Participant.Status, TripParticipantStatus.NotConfirmed,
                                          StringComparison.OrdinalIgnoreCase))
                .Select(i => new PendingInvitationDto
                {
                    TripId = i.Participant.TripId,
                    TripName = i.Participant.Trip.Name,
                    Location = i.Participant.Trip.Location,
                    StartDate = FormatDate(i.Participant.Trip.StartDate),
                    EndDate = FormatDate(i.Participant.Trip.EndDate),
                    InvitedBy = i.Participant.Trip.Owner?.FirstName ?? "The organiser",
                    Role = i.Participant.Role,
                    InvitedAt = i.CreatedAt
                })
                .ToList();
        }

        public async Task<int> LinkInvitationsToNewUserAsync(
            Guid userId, string email, CancellationToken ct = default)
        {
            // Someone invited before they had an account has participant rows with a
            // null UserId. Binding them now is what makes the trip appear once they
            // sign in, instead of the invitation looking lost.
            var linked = await _trips.LinkParticipantsByEmailAsync(email, userId, ct);

            if (linked > 0)
                _logger.LogInformation(
                    "Linked {Count} participant row(s) to new user {UserId}.", linked, userId);

            return linked;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task IssueAndSendAsync(TripParticipant participant, CancellationToken ct)
        {
            await _invitations.InvalidateForParticipantAsync(participant.Id, ct);

            var raw = _secureTokens.Generate();

            await _invitations.AddAsync(new TripInvitation
            {
                TripParticipantId = participant.Id,
                TokenHash = _secureTokens.Hash(raw),
                ExpiresAt = Now.AddDays(InvitationValidDays),
                SentAt = Now
            }, ct);

            var trip = participant.Trip;
            var link = FrontendLinks.TripInvitation(_settings, raw);

            await _email.TrySendAsync(AuthEmailTemplates.TripInvitation(
                toAddress: participant.Email,
                inviterName: trip.Owner?.FirstName ?? "A friend",
                tripName: trip.Name,
                location: trip.Location,
                dateRange: $"{FormatDate(trip.StartDate)} → {FormatDate(trip.EndDate)}",
                role: participant.Role,
                link: link,
                validDays: InvitationValidDays), _logger, ct);
        }

        /// <summary>Marks the invitation answered and updates the seat behind it.</summary>
        private async Task ApplyResponseAsync(
            TripInvitation invitation, User? user, bool accepted, CancellationToken ct)
        {
            invitation.RespondedAt = Now;
            await _invitations.UpdateAsync(invitation, ct);

            await ApplyToParticipantAsync(invitation.Participant, user, accepted, ct);
        }

        private async Task ApplyToParticipantAsync(
            TripParticipant participant, User? user, bool accepted, CancellationToken ct)
        {
            participant.Status = accepted
                ? TripParticipantStatus.Confirmed
                : TripParticipantStatus.Declined;

            // Binding the account is what turns a seat into real access — TripAccess
            // matches on UserId, not on the email.
            if (accepted && user is not null) participant.UserId = user.Id;

            await _trips.UpdateParticipantAsync(participant, ct);

            await NotifyOwnerAsync(participant, accepted, ct);

            _logger.LogInformation(
                "Participant {ParticipantId} {Outcome} the invitation to trip {TripId}.",
                participant.Id, accepted ? "accepted" : "declined", participant.TripId);
        }

        private async Task NotifyOwnerAsync(
            TripParticipant participant, bool accepted, CancellationToken ct)
        {
            var trip = participant.Trip ?? await _trips.GetByIdAsync(participant.TripId, ct);
            if (trip is null) return;

            // A trip waiting for a new owner has nobody to tell.
            if (trip.OwnerId is null) return;

            var owner = trip.Owner ?? await _users.GetByIdAsync(trip.OwnerId.Value, ct);
            if (owner is null) return;

            var link = FrontendLinks.TripDetail(_settings, trip.Id);

            await _email.TrySendAsync(AuthEmailTemplates.InvitationAnswered(
                ownerEmail: owner.Email,
                inviteeName: string.IsNullOrWhiteSpace(participant.Nickname)
                    ? participant.Email
                    : participant.Nickname,
                tripName: trip.Name,
                accepted: accepted,
                tripLink: link), _logger, ct);
        }

        /// <summary>Loads by token and reports why it is unusable, when it is.</summary>
        private async Task<(TripInvitation? Invitation, Error? Error)> LoadAsync(
            string rawToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return (null, ErrorCodes.InvitationNotFound);

            var invitation = await _invitations.GetByHashAsync(_secureTokens.Hash(rawToken), ct);

            return invitation is null
                ? (null, ErrorCodes.InvitationNotFound)
                : (invitation, null);
        }

        /// <summary>Which of the several ways an invitation can be closed applies here.</summary>
        private Error ClosedReason(TripInvitation invitation) =>
            invitation.RespondedAt is not null ? ErrorCodes.InvitationAlreadyAnswered
            : invitation.InvalidatedAt is not null ? ErrorCodes.InvitationWithdrawn
            : ErrorCodes.InvitationExpired;

        private static string FormatDate(DateTime value) =>
            value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
