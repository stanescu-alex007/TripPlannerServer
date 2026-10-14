using TripPlanner.Core.Entities;

namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Owns how second-factor credentials are stored: the TOTP secret encrypted at rest,
    /// recovery codes hashed. No caller ever handles either stored form.
    ///
    /// <para>
    /// The TOTP arithmetic itself stays in <see cref="ITwoFactorService"/>; this is the
    /// persistence side of the same feature.
    /// </para>
    /// </summary>
    public interface ITwoFactorCredentialService
    {
        /// <summary>
        /// Stores an already-confirmed secret and switches 2FA on. Returns the recovery
        /// codes in plain form — the only time they exist unhashed.
        /// </summary>
        /// <param name="confirmedTimeStep">
        /// The step of the code the user just proved with, recorded as spent so it cannot
        /// be replayed at the next sign-in.
        /// </param>
        Task<IReadOnlyList<string>> EnableAsync(
            User user, string secret, long confirmedTimeStep, CancellationToken ct = default);

        /// <summary>Clears the secret and every recovery code.</summary>
        Task DisableAsync(User user, CancellationToken ct = default);

        /// <summary>Issues a fresh set, invalidating every previous code.</summary>
        Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Accepts a current authenticator code, or an unused recovery code — which is
        /// burned in the process.
        /// </summary>
        Task<bool> VerifyAsync(User user, string code, CancellationToken ct = default);

        /// <summary>
        /// Encrypts secrets written before encryption at rest existed. Idempotent; returns
        /// how many were converted.
        /// </summary>
        Task<int> ProtectLegacySecretsAsync(CancellationToken ct = default);
    }
}
