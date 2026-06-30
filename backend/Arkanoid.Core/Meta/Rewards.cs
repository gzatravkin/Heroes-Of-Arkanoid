namespace Arkanoid.Core.Meta;

public sealed class RewardResult
{
    public bool FirstClear     { get; init; }
    public int  ExpGained      { get; init; }
    /// <summary>Coins gained on first clear (economy rework): Sparks (gear) + Insight (mastery) always,
    /// Souls (spell/hero) on a biome boss. Includes StarBonusSouls on first clear.</summary>
    public int  SparksGained   { get; init; }
    public int  SoulsGained    { get; init; }
    public int  InsightGained  { get; init; }
    public int  NewLevel       { get; init; }
    public bool LeveledUp      { get; init; }
    /// <summary>Best star rating (1–3) earned this run: 3=full HP, 2=≥2HP, 1=any win.</summary>
    public int  LevelStars     { get; init; }
    /// <summary>Souls awarded for newly achieving a higher star tier on this level. 0 on re-clears at same/lower stars.</summary>
    public int  StarBonusSouls { get; init; }
    /// <summary>Character id unlocked by this clear (boss firsts), or null.</summary>
    public string? CharacterUnlocked { get; init; }
    /// <summary>Spell ids newly added to the owned pool by this clear (docs/04 §5 permanent unlock).</summary>
    public List<string> SpellsUnlocked { get; init; } = new();
    /// <summary>Hotbar slots gained by this clear (docs/04 §4.1 — loadout grows with progression).</summary>
    public int SlotsUnlocked { get; init; }
    /// <summary>Meta features unlocked by this clear (display names), for the "🔓 Unlocked" reward beat.</summary>
    public List<string> FeaturesUnlocked { get; init; } = new();
}

/// <summary>Result of per-battle hero-XP accrual (§5.3) — surfaced for the "Hero leveled up!" beat.</summary>
public sealed class HeroXpResult
{
    public string HeroId    { get; init; } = "";
    public int    XpGained  { get; init; }
    public int    NewLevel  { get; init; }
    public bool   LeveledUp { get; init; }
    public int    Stars     { get; init; }
    public int    TokensGranted { get; init; }
}

public static class Rewards
{
    /// <summary>
    /// Boss clears unlock the next character (docs/04 §3: "reruns earn new ones").
    /// Existing saves already persist all four unlocked — only fresh profiles earn.
    /// </summary>
    private static readonly Dictionary<string, string> CharacterUnlocks = new()
    {
        ["hell-boss"]    = "paladin",
        ["caverns-boss"] = "engineer",
        ["village-boss"] = "necromancer",
    };

    /// <summary>
    /// Biome-boss first-clears permanently unlock shared-pool spells (docs/04 §5: "clearing grants a
    /// permanent unlock — a new spell enters the global pool"). Signatures are excluded (exclusive,
    /// auto-owned by their character). hell-boss also grants the Fire Mage capstone Phoenix so the
    /// starter has a path to its 5th spell. Tunable content.
    /// </summary>
    private static readonly Dictionary<string, string[]> SpellUnlocks = new()
    {
        ["hell-boss"]    = new[] { "phoenix", "spear", "duplicate", "penetration", "lastday" },
        ["caverns-boss"] = new[] { "lightning", "rocket", "radiation", "magnet" },
        ["village-boss"] = new[] { "decay", "drain", "golem", "mage" },
    };

    /// <summary>Hotbar slots grow on biome-boss clears (docs/04 §4.1), capped at the hotbar max.</summary>
    private const int MaxSpellSlots = 4; // economy rework §3: signature + up to 3 flex

    // Economy rework §10 — the 3-coin campaign faucet (tunable).
    public const int InsightPerClear = 20; // mastery coin, every first-clear
    public const int SparksPerClear  = 12; // gear coin, every first-clear
    public const int SoulsPerBoss    = 40; // spell/hero coin, biome-boss first-clear

    /// <summary>Flat hero-XP for winning a battle, on top of blocks destroyed (§5.3). Tunable.</summary>
    private const int HeroWinBonusXp = 25;

    /// <summary>Hero Tokens granted per win toward ★ ascension (§5.4). Bosses pay the larger purse.
    /// (§5.4 names the currency but not its source; a win-drip is the MVP source — tunable.)</summary>
    private const int HeroTokensPerWin  = 3;  // raised from 1 (balance: ~12 wins = 1 spell roll)
    private const int HeroTokensPerBoss = 5;

    /// <summary>Hero-XP accrual (design §5.3): unlike account rewards, this runs EVERY battle with the
    /// selected hero (re-clears included) — XP = blocks destroyed + a win bonus — and consumes the
    /// §5.3 curve (xpToNext = 80×1.12^(lvl-1)) to level the hero, capped at Lvl 30. Also drips Hero
    /// Tokens toward ★ ascension (§5.4) on a win. Mutates p in-place.</summary>
    public static HeroXpResult GrantHeroXp(Profile p, string heroId, int blocksDestroyed, bool won, bool isBoss = false)
    {
        if (string.IsNullOrWhiteSpace(heroId)) heroId = "fire_mage";
        if (!p.HeroProgress.TryGetValue(heroId, out var hp))
        {
            hp = new HeroProgress();
            p.HeroProgress[heroId] = hp;
        }

        int startLevel = hp.Level;
        int xp = System.Math.Max(0, blocksDestroyed) + (won ? HeroWinBonusXp : 0);
        hp.Exp += xp;

        // Level-up loop against the §5.3 curve; cap at Lvl 30.
        while (hp.Level < 30 && hp.Exp >= StatResolver.XpToNext(hp.Level))
        {
            hp.Exp -= StatResolver.XpToNext(hp.Level);
            hp.Level++;
        }
        if (hp.Level >= 30) { hp.Level = 30; hp.Exp = 0; }

        // Economy rework: hero ★ now comes from DUPLICATE HERO ROLLS (HeroTokens retired). A win still
        // pays the hero/spell coin (Souls) so battles feed the roll economy; boss wins pay more.
        int tokens = won ? (isBoss ? HeroTokensPerBoss : HeroTokensPerWin) : 0;
        if (tokens > 0) Wallet.Add(p, Currency.Souls, tokens);

        return new HeroXpResult
        {
            HeroId        = heroId,
            XpGained      = xp,
            NewLevel      = hp.Level,
            LeveledUp     = hp.Level > startLevel,
            Stars         = hp.Stars,
            TokensGranted = tokens,
        };
    }

    /// <summary>Souls bonus for reaching each star tier for the first time (per new tier, cumulative).</summary>
    private static int StarTierBonus(int tier) => tier switch { 2 => 20, 3 => 40, _ => 0 };

    /// <summary>
    /// Grants first-clear rewards to <paramref name="p"/> for completing <paramref name="levelId"/>.
    /// Idempotent: subsequent calls with the same levelId return FirstClear=false but still
    /// grant star-upgrade Souls if a higher rating is achieved on the re-clear.
    /// Mutates <paramref name="p"/> in-place.
    /// </summary>
    /// <param name="hp">Player HP at win — drives the 1/2/3-star rating (3=full HP≥3, 2=HP≥2, 1=any).</param>
    /// <param name="treasureBonus">
    /// Extra crystals from equipped treasure items (from GameInstance.ItemTreasureBonus).
    /// Only applied on first clear.
    /// </param>
    public static RewardResult GrantLevelCompletion(Profile p, string levelId, ProgressionConfig cfg,
                                                     int treasureBonus = 0, int ballsDropped = 0)
    {
        // Stars — computed on every win (re-clears can still improve the tier).
        // 0 drops = perfect ★★★, 1-2 drops = ★★☆, 3+ drops = ★☆☆.
        int earnedStars = ballsDropped == 0 ? 3 : ballsDropped <= 2 ? 2 : 1;
        int prevStars   = p.LevelStars.GetValueOrDefault(levelId, 0);
        int newStars    = System.Math.Max(prevStars, earnedStars);
        p.LevelStars[levelId] = newStars;
        int starBonusSouls = 0;
        for (int tier = prevStars + 1; tier <= earnedStars; tier++)
            starBonusSouls += StarTierBonus(tier);
        if (starBonusSouls > 0) Wallet.Add(p, Currency.Souls, starBonusSouls);

        if (p.CompletedLevels.Contains(levelId))
        {
            return new RewardResult
            {
                FirstClear     = false,
                ExpGained      = 0,
                NewLevel       = p.Level,
                LeveledUp      = false,
                LevelStars     = newStars,
                StarBonusSouls = starBonusSouls,
            };
        }

        // Economy rework §10: the 3-coin faucet. Every first-clear pays Insight (mastery) + Sparks (gear,
        // + any treasure-item bonus); a biome boss also pays Souls (spell/hero rolls). Prestige loops
        // scale the payout (+50%/tier). The retired Points/Crystals/CampaignGold are no longer granted.
        bool isBoss = levelId.EndsWith("-boss");
        int sparksGained  = SparksPerClear + treasureBonus;
        int insightGained = InsightPerClear;
        int soulsGained   = isBoss ? SoulsPerBoss : 0;
        if (p.PrestigeTier > 0)
        {
            sparksGained  = PrestigeService.ScaleReward(sparksGained, p.PrestigeTier);
            insightGained = PrestigeService.ScaleReward(insightGained, p.PrestigeTier);
            soulsGained   = PrestigeService.ScaleReward(soulsGained, p.PrestigeTier);
        }

        p.CompletedLevels.Add(levelId);
        p.Exp     += cfg.ExpRewardPerLevel;
        p.Sparks  += sparksGained;
        p.Insight += insightGained;
        p.Souls   += soulsGained;

        int startingLevel = p.Level;
        // Level-up loop: consume EXP thresholds until insufficient.
        while (p.Exp >= cfg.ExpToLevel(p.Level))
        {
            p.Exp -= cfg.ExpToLevel(p.Level);
            p.Level++;
        }

        // Boss first-clears make the next hero ROLLABLE (economy rework §4): the hero enters the C2 hero
        // pool and stays locked until its first card is rolled (RollService.RollHero).
        string? characterUnlocked = null;
        if (CharacterUnlocks.TryGetValue(levelId, out var charId)
            && !p.UnlockedCharacters.Contains(charId)
            && !p.HeroPool.Contains(charId))
        {
            p.HeroPool.Add(charId);
            characterUnlocked = charId; // surfaced to the UI as "now rollable"
        }

        // Boss first-clears permanently unlock shared-pool spells + grow the hotbar (docs/04 §4.1/§5).
        var spellsUnlocked = new List<string>();
        int slotsUnlocked = 0;
        if (SpellUnlocks.TryGetValue(levelId, out var spellIds))
        {
            foreach (var id in spellIds)
            {
                if (p.SpellLevels.ContainsKey(id)) continue; // already owned
                p.SpellLevels[id] = 1;
                spellsUnlocked.Add(id);
            }
            if (p.UnlockedSpellSlots < MaxSpellSlots)
            {
                p.UnlockedSpellSlots++;
                slotsUnlocked = 1;
            }
        }

        // Include the star bonus in soulsGained so the UI can display the total (star bonus was
        // already added to p.Souls above via Wallet.Add; here we just fold it into the shown total).
        soulsGained += starBonusSouls;

        return new RewardResult
        {
            FirstClear     = true,
            ExpGained      = cfg.ExpRewardPerLevel,
            SparksGained   = sparksGained,
            SoulsGained    = soulsGained,
            InsightGained  = insightGained,
            NewLevel       = p.Level,
            LeveledUp      = p.Level > startingLevel,
            LevelStars     = earnedStars,
            StarBonusSouls = starBonusSouls,
            CharacterUnlocked = characterUnlocked,
            SpellsUnlocked = spellsUnlocked,
            SlotsUnlocked  = slotsUnlocked,
            FeaturesUnlocked = FeatureGates.UnlockedBy(levelId).Select(FeatureGates.DisplayName).ToList(),
        };
    }
}
