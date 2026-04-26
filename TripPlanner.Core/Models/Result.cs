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

    public class Result<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public static Result<T> Ok(T data)
        {
            return new Result<T> { Success = true, Data = data };
        }

        public static Result<T> Fail(string message)
        {
            return new Result<T> { Success = false, Message = message };
        }
    }
}