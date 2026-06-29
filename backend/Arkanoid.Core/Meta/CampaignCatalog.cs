using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class CampaignNode
{
    [JsonPropertyName("id")]    public string Id    { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("biome")] public string Biome { get; set; } = "";
}

public sealed class CampaignCatalog : Catalog<CampaignNode>
{
    // The campaign is a LINEAR chain (economy rework, docs/2026-06-14): list ORDER is the sequence,
    // so we keep the ordered list to resolve a node's predecessor for unlock checks.
    private readonly List<CampaignNode> _ordered;

    private CampaignCatalog(List<CampaignNode> nodes) : base(nodes, n => n.Id) { _ordered = nodes; }

    /// <summary>All campaign nodes in sequence order.</summary>
    public IEnumerable<CampaignNode> Nodes => _ordered;

    /// <summary>A node is unlocked when it's the first level, or the PREVIOUS level in the chain is
    /// completed. Linear progression — no dependency graph (economy rework, docs/2026-06-14).</summary>
    public bool IsUnlocked(CampaignNode node, ISet<string> completed)
    {
        int i = _ordered.FindIndex(n => n.Id == node.Id);
        return i <= 0 || completed.Contains(_ordered[i - 1].Id);
    }

    private sealed class Dto
    {
        [JsonPropertyName("nodes")] public List<CampaignNode> Nodes { get; set; } = new();
    }

    public static CampaignCatalog FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json)
            ?? throw new InvalidOperationException("Invalid campaign JSON");
        return new CampaignCatalog(dto.Nodes);
    }

    public static CampaignCatalog FromFile(string path) => FromJson(File.ReadAllText(path));
}
