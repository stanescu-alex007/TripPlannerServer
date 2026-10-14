using Microsoft.Extensions.Logging;
using TripPlanner.BusinessLogic.Email;
using TripPlanner.Core.Common;
using TripPlanner.Core.Interfaces.IRepositories;
using TripPlanner.Core.Interfaces.IServices;
using TripPlanner.Core.Models;

namespace TripPlanner.BusinessLogic.Services
{
    /// <summary>Where contact-page messages are delivered (<c>Contact:InboxAddress</c>).</summary>
    public sealed record ContactSettings(string? InboxAddress);

    public class ContactService : IContactService
    {
        private static readonly Dictionary<string, string> SubjectLabels = new()
        {
            ["bug"] = "Bug report",
            ["feature"] = "Feature request",
            ["question"] = "General question",
            ["partnership"] = "Partnership",
            ["other"] = "Other",
        };

        private readonly IUserRepository _users;
        private readonly IEmailSender _email;
        private readonly ContactSettings _settings;
        private readonly ILogger<ContactService> _logger;

        public ContactService(
            IUserRepository users,
            IEmailSender email,
            ContactSettings settings,
            ILogger<ContactService> logger)
        {
            _users = users;
            _email = email;
            _settings = settings;
            _logger = logger;
        }

        public async Task<Result> SendAsync(Guid userId, ContactRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.InboxAddress))
            {
                _logger.LogError("Contact message dropped: Contact:InboxAddress is not configured.");
                return Result.Fail(ErrorCodes.ContactUnavailable);
            }

            var user = await _users.GetByIdAsync(userId, ct);
            if (user is null) return Result.Fail(ErrorCodes.UserNotFound);

            var topic = SubjectLabels.GetValueOrDefault(request.Subject, "Other");
            var message = ContactEmailTemplates.ContactMessage(
                _settings.InboxAddress, user, topic, request.Message.Trim());

            // Not TrySendAsync: delivering the message is the whole operation, so the
            // user must hear about a failure rather than be told it was sent.
            try
            {
                await _email.SendAsync(message, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "Could not deliver a contact message from user {UserId}.", userId);
                return Result.Fail(ErrorCodes.ContactUnavailable);
            }

            _logger.LogInformation("Contact message ({Topic}) sent by user {UserId}.", topic, userId);
            return Result.Ok();
        }
    }
}
