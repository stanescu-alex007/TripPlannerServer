using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IContactService
    {
        /// <summary>
        /// Emails the message to the team inbox, with Reply-To set to the sender's
        /// account address so the team can answer straight from their mail client.
        /// </summary>
        Task<Result> SendAsync(Guid userId, ContactRequest request, CancellationToken ct = default);
    }
}
