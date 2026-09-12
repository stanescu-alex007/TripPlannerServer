namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IPasswordService
    {
        string Hash(string password);

        bool Verify(string password, string hash);

        /// <summary>
        /// Runs the same hashing work against a throwaway hash and always returns false.
        /// Called when the email is unknown, so login takes the same time whether the
        /// account exists or not and cannot be timed to enumerate addresses.
        /// </summary>
        bool VerifyDummy(string password);
    }
}
