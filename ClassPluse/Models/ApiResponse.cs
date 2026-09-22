namespace ClassPluse.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(string message, T? data = default)
        {
            return new ApiResponse<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponse<T> Error(string message)
        {
            return new ApiResponse<T> { Success = false, Message = message };
        }
    }

    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse Ok(string message)
        {
            return new ApiResponse { Success = true, Message = message };
        }

        new public static ApiResponse Error(string message)
        {
            return new ApiResponse { Success = false, Message = message };
        }
    }
}
