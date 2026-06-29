using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

/// <summary>
/// Economy rework (docs/2026-06-14): the campaign is a flat ORDERED list of levels — no graph fields
/// (no x/y/requires/type), no shop nodes. List order is the sequence; unlock = previous level cleared.
/// Asserts the shape of the shipped <c>config/campaign.json</c>.
/// </summary>
public class CampaignTopologyTests
{
    private static string CampaignJson()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var p = Path.Combine(dir, "config", "campaign.json");
            if (File.Exists(p)) return File.ReadAllText(p);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Cannot find config/campaign.json — run tests from within the repo.");
    }

    [Fact]
    public void Campaign_IsFlatOrderedList_NoGraphFields_NoShops()
    {
        using var doc = JsonDocument.Parse(CampaignJson());
        var nodes = doc.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        Assert.NotEmpty(nodes);

        foreach (var n in nodes)
        {
            // Each level carries only id + label + biome.
            Assert.True(n.TryGetProperty("id", out _));
            Assert.True(n.TryGetProperty("label", out _));
            Assert.True(n.TryGetProperty("biome", out _));
            // The linear-graph/layout cruft is gone.
            foreach (var dead in new[] { "x", "y", "requires", "type" })
                Assert.False(n.TryGetProperty(dead, out _), $"node should not carry '{dead}' anymore");
        }

        // Ids are unique (they key save-completion + level routing).
        var ids = nodes.Select(n => n.GetProperty("id").GetString()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Catalog_UnlocksLinearlyByOrder()
    {
        var cat = Arkanoid.Core.Meta.CampaignCatalog.FromJson(CampaignJson());
        var list = cat.Nodes.ToList();
        var done = new System.Collections.Generic.HashSet<string>();
        Assert.True(cat.IsUnlocked(list[0], done));   // first level always open
        Assert.False(cat.IsUnlocked(list[1], done));  // second locked until first cleared
        done.Add(list[0].Id);
        Assert.True(cat.IsUnlocked(list[1], done));
    }
}
