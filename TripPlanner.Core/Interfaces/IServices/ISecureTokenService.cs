namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Generates and hashes the opaque tokens used for refresh tokens, email
    /// verification and password resets.
    /// </summary>
    public interface ISecureTokenService
    {
        /// <summary>
        /// A new cryptographically random token, URL-safe so it can be dropped straight
        /// into an email link.
        /// </summary>
        string Generate();

        /// <summary>
        /// SHA-256 of a raw token, hex-encoded. Only the hash is ever stored.
        /// <para>
        /// Deliberately fast and unsalted, unlike password hashing: these tokens are
        /// already 256 bits of entropy, so there is nothing to brute-force, and the hash
        /// has to be reproducible in order to look the row up by equality.
        /// </para>
        /// </summary>
        string Hash(string rawToken);
    }
}
