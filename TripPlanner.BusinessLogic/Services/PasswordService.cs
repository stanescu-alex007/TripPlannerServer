using Konscious.Security.Cryptography;
using System.Text;
using TripPlanner.Core.Interfaces.IServices;


namespace TripPlanner.BusinessLogic.Services
{
  
    public class PasswordService : IPasswordService
    {
        public string Hash(string password)
        {
            var salt = Encoding.UTF8.GetBytes("super-secret-salt"); // 🔥 mai târziu random

            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 2,
                Iterations = 4,
                MemorySize = 1024 * 128
            };

            var hash = argon2.GetBytes(32);

            return Convert.ToBase64String(hash);
        }

        public bool Verify(string password, string hash)
        {
            var newHash = Hash(password);
            return newHash == hash;
        }
    }
}
