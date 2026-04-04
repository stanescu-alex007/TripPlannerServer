namespace TripPlanner.Core.Models
{
    public class Result
    {
        public bool Success { get; set; }
        public string? Message { get; set; }

        public static Result Ok()
        {
            return new Result { Success = true };
        }

        public static Result Fail(string message)
        {
            return new Result
            {
                Success = false,
                Message = message
            };
        }
    }
}