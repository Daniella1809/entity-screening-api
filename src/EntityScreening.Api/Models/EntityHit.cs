namespace EntityScreening.Api.Models;

public class EntityHit
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string?> Attributes { get; set; } = new();
}
