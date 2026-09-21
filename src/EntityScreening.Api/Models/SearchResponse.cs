namespace EntityScreening.Api.Models;

public class SearchResponse
{
    public string Source { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public int Hits { get; set; }

    public DateTime RetrievedAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<EntityHit> Results { get; set; } = new List<EntityHit>();
}
