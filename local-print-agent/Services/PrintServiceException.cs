namespace local_print_agent.Services;

public class PrintServiceException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public PrintServiceException(string errorCode, string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}