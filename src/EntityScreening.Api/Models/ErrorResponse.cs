namespace EntityScreening.Api.Models;

public class ErrorResponse
{
    public int Status { get; set; }

    public string Error { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<string>? Details { get; set; }

    public ErrorResponse() { }

    public ErrorResponse(int status, string error, string message, IReadOnlyList<string>? details = null)
    {
        Status = status;
        Error = error;
        Message = message;
        Details = details;
    }
}
