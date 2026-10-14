using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>Creating an account and proving ownership of the address behind it.</summary>
    public interface IRegistrationService
    {
        Task<Result> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

        Task<Result> VerifyEmailAsync(string rawToken, CancellationToken ct = default);

        /// <summary>Always reports success, so it cannot be used to probe for accounts.</summary>
        Task<Result> ResendVerificationAsync(string email, CancellationToken ct = default);
    }
}
