using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

/// <summary>
/// Structural design invariant for Witchland (docs/12 + docs/2026-06-16-village-ghost-rework-spec.md):
/// ghost-layer content can ONLY be cleared by a phased ball, so ANY village level that contains ghost
/// blocks (village_ghost) OR a ghost necromant (village_necromant_ghost) MUST contain at least one portal
/// (village_portal). A regular necromant raises regular corpses on the physical layer and needs no portal.
///
/// This is the test that catches the village-6/7 "ghost blocks but no portal → unclearable" drift, which
/// the AllLevelsWinnableTests chip-assist cheat silently masked (it damages every block regardless of phase).
/// </summary>
public class VillagePhaseInvariantTests
{
    private const string Ghost          = "village_ghost";
    private const string GhostNecromant = "village_necromant_ghost";
    private const string Portal         = "village_portal";

    private static string ConfigRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "config", "blocks.json")))
                return Path.Combine(dir, "config");
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Cannot find config/blocks.json — run tests from within the repo.");
    }

    public static IEnumerable<object[]> VillageLevels()
    {
        var dir = Path.Combine(ConfigRoot(), "levels");
        foreach (var path in Directory.GetFiles(dir, "village-*.json").OrderBy(p => p))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            if (id == "village-ghost") continue; // legacy non-campaign test floor
            yield return new object[] { id };
        }
    }

    [Theory]
    [MemberData(nameof(VillageLevels))]
    public void GhostOrNecromantLevel_HasPortal(string levelId)
    {
        var path = Path.Combine(ConfigRoot(), "levels", $"{levelId}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));

        // Block ids actually referenced by this level = the legend values.
        var used = doc.RootElement.TryGetProperty("legend", out var legend)
            ? legend.EnumerateObject().Select(p => p.Value.GetString()).ToHashSet()
            : new HashSet<string?>();

        bool needsPhasing = used.Contains(Ghost) || used.Contains(GhostNecromant);
        if (!needsPhasing) return; // no ghost-layer content → no portal required

        Assert.True(used.Contains(Portal),
            $"{levelId} uses {(used.Contains(Ghost) ? "ghost blocks" : "")}" +
            $"{(used.Contains(Ghost) && used.Contains(GhostNecromant) ? " + " : "")}" +
            $"{(used.Contains(GhostNecromant) ? "a ghost necromant" : "")} but has NO portal — " +
            "the ball can never phase in, so that ghost-layer content is unclearable. Add a village_portal.");
    }
}
