using Microsoft.Extensions.Logging;
using TripPlanner.Core.Interfaces.IServices;

namespace TripPlanner.BusinessLogic.Email
{
    internal static class EmailSenderExtensions
    {
        /// <summary>
        /// Sends without letting a delivery failure fail the operation behind it. By the
        /// time a notification goes out, the change it describes is already written, so
        /// reporting failure to the caller would be a lie.
        /// </summary>
        public static async Task TrySendAsync(
            this IEmailSender sender, EmailMessage message, ILogger logger, CancellationToken ct)
        {
            try
            {
                await sender.SendAsync(message, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex,
                    "Could not deliver '{Subject}' to {Recipient}. The operation itself succeeded.",
                    message.Subject, message.ToAddress);
            }
        }
    }
}
