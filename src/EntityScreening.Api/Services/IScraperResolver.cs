namespace EntityScreening.Api.Services;

public interface IScraperResolver
{
    IReadOnlyCollection<string> AvailableSources { get; }

    IScraperService Resolve(string sourceKey);
}

public class SourceNotFoundException : Exception
{
    public IReadOnlyCollection<string> AvailableSources { get; }

    public SourceNotFoundException(string requestedSource, IReadOnlyCollection<string> availableSources)
        : base($"La fuente '{requestedSource}' no existe. Fuentes disponibles: {string.Join(", ", availableSources)}.")
    {
        AvailableSources = availableSources;
    }
}
