using EntityScreening.Api.Models;

namespace EntityScreening.Api.Services;

public interface IScraperService
{
    string SourceKey { get; }

    string DisplayName { get; }

    Task<SearchResponse> SearchAsync(string entityName, CancellationToken cancellationToken = default);
}
