namespace TripPlanner.Core.Interfaces.IServices
{
    /// <summary>
    /// Encrypts small secrets that have to be read back later — unlike passwords, which
    /// are only ever hashed.
    ///
    /// <para>
    /// <c>purpose</c> is bound into the ciphertext as authenticated data. A value copied
    /// into another row, or reused for another purpose, fails to decrypt instead of
    /// quietly working there.
    /// </para>
    /// </summary>
    public interface ISecretProtector
    {
        string Protect(string plaintext, string purpose);

        /// <summary>
        /// Null when the value is not in protected form, has been altered, was bound to a
        /// different purpose, or was written with a key that is no longer configured.
        /// </summary>
        string? Unprotect(string protectedValue, string purpose);

        /// <summary>
        /// The prefix every protected value starts with. Lets a query find values written
        /// before protection existed without decrypting anything.
        /// </summary>
        string Marker { get; }
    }
}
