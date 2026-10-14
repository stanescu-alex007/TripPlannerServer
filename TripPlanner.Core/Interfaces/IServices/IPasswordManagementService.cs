using TripPlanner.Core.Models;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>Recovering a forgotten password and changing a known one.</summary>
    public interface IPasswordManagementService
    {
        /// <summary>Always reports success, so it cannot be used to probe for accounts.</summary>
        Task<Result> ForgotPasswordAsync(string email, CancellationToken ct = default);

        /// <summary>Sets the new password and ends every session, the caller's included.</summary>
        Task<Result> ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct = default);

        /// <summary>Ends every other session and returns a fresh one for the caller.</summary>
        Task<Result<AuthTokens>> ChangePasswordAsync(
            Guid userId, string currentPassword, string newPassword,
            RequestContext context, CancellationToken ct = default);
    }
}
