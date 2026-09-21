namespace EntityScreening.Api.Services;

public class ScraperResolver : IScraperResolver
{
    private readonly Dictionary<string, IScraperService> _scrapers;

    public ScraperResolver(IEnumerable<IScraperService> scrapers)
    {
        _scrapers = scrapers.ToDictionary(
            s => s.SourceKey.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> AvailableSources => _scrapers.Keys.ToList();

    public IScraperService Resolve(string sourceKey)
    {
        var key = (sourceKey ?? string.Empty).Trim().ToLowerInvariant();

        if (_scrapers.TryGetValue(key, out var scraper))
        {
            return scraper;
        }

        throw new SourceNotFoundException(sourceKey ?? string.Empty, AvailableSources);
    }
}
