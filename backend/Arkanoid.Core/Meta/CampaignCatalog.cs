using System.Text.Json;
using System.Text.Json.Serialization;
namespace Arkanoid.Core.Meta;

public sealed class CampaignNode
{
    [JsonPropertyName("id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("label")]    public string Label    { get; set; } = "";
    [JsonPropertyName("biome")]    public string Biome    { get; set; } = "";
    [JsonPropertyName("x")]        public double X        { get; set; }
    [JsonPropertyName("y")]        public double Y        { get; set; }
    [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
}

public sealed class CampaignCatalog
{
    private readonly List<CampaignNode> _nodes;
    private CampaignCatalog(IEnumerable<CampaignNode> nodes) => _nodes = new List<CampaignNode>(nodes);

    public IEnumerable<CampaignNode> Nodes => _nodes;

    /// <summary>A node is unlocked when every id in Requires appears in completed (empty Requires => always unlocked).</summary>
    public bool IsUnlocked(CampaignNode node, ISet<string> completed)
        => node.Requires.Count == 0 || node.Requires.All(completed.Contains);

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
