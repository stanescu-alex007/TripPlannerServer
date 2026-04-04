namespace TripPlanner.Core.Interfaces.IServices
{
    public interface IPasswordService
    {
        string Hash(string password);
        bool Verify(string password, string hash);
    }
}
