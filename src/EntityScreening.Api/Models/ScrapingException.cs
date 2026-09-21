namespace EntityScreening.Api.Models;

public class ScrapingException : Exception
{
    public string SourceKey { get; }

    public ScrapingException(string source, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        SourceKey = source;
    }
}
