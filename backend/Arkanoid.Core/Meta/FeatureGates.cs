using System.Collections.Generic;
using System.Linq;
namespace Arkanoid.Core.Meta;

/// <summary>The meta features that unlock progressively as the campaign advances (so new players aren't
/// dumped into every system at once). Core meta (heroes/loadout/items/skills/settings) is always open.</summary>
public enum Feature { Daily, Cards, Season, Modules, League, Prestige }

/// <summary>
/// Campaign-progress gates for the social/economy features. A feature unlocks when its required campaign
/// level has been cleared (empty = unlocked from the start). One source of truth for the menu (what's
/// locked + the requirement) and for reward-time "🔓 unlocked" announcements.
/// </summary>
public static class FeatureGates
{
    /// <summary>Feature → required completed level id ("" = always available). Ordered by reveal pacing.</summary>
    private static readonly (Feature Feature, string Req)[] Gates =
    {
        (Feature.Daily,    ""),            // from the start — gentle onboarding
        (Feature.Cards,    "hell-2"),      // a couple of levels in
        (Feature.Season,   "hell-3"),
        (Feature.Modules,  "hell-boss"),   // first biome boss
        (Feature.League,   "caverns-boss"),// the competitive ladder, once you have some power
        (Feature.Prestige, "heaven-boss"), // beat the campaign
    };

    public static IReadOnlyList<Feature> All => Gates.Select(g => g.Feature).ToList();

    public static string RequiredLevel(Feature f) => Gates.First(g => g.Feature == f).Req;

    public static bool IsUnlocked(Feature f, ISet<string> completed)
    {
        var req = RequiredLevel(f);
        return req.Length == 0 || completed.Contains(req);
    }

    /// <summary>Features whose gate is exactly <paramref name="levelId"/> — i.e. clearing it unlocks them.</summary>
    public static IReadOnlyList<Feature> UnlockedBy(string levelId)
        => Gates.Where(g => g.Req == levelId).Select(g => g.Feature).ToList();

    /// <summary>Player-facing feature name (for the menu + reward announcements).</summary>
    public static string DisplayName(Feature f) => f switch
    {
        Feature.Daily    => "Daily Missions",
        Feature.Cards    => "Cards",
        Feature.Season   => "Season Festival",
        Feature.Modules  => "Modules",
        Feature.League   => "League",
        Feature.Prestige => "Prestige",
        _ => f.ToString(),
    };
}
