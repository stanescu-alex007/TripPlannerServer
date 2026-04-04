namespace TripPlanner.Core.Models
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }

        public AuthResult() { }

        public AuthResult(string accessToken, string refreshToken)
        {
            Success = true;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }

        public static AuthResult Fail(string message)
        {
            return new AuthResult
            {
                Success = false,
                Message = message
            };
        }
    }
}