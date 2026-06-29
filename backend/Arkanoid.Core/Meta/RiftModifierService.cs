using System.Collections.Generic;
using System.Linq;
using Arkanoid.Core.Sim;

namespace Arkanoid.Core.Meta;

/// <summary>One §8 rift run-modifier (presented 1-of-3 after each rift level; applies for the rest of the run).</summary>
public sealed record RiftModifier(string Id, string Name, string Desc, string Type, bool Stackable);

/// <summary>
/// §8 Rift modifier pool (design 2026-06-13 §8). Pure logic: the catalog of 10, the seeded 1-of-3 draft, the
/// immediate on-pick effects (heal / max-HP / reward / difficulty — applied to the <see cref="DungeonRun"/>),
/// and the per-level <see cref="ApplyToGame"/> that re-applies the persistent stat effects each rift level.
/// No file I/O. The run's power comes from the permanent build + these picks (no permanent draft).
/// </summary>
public static class RiftModifierService
{
    /// <summary>The 10 approved modifiers (design §8 table).</summary>
    public static readonly IReadOnlyList<RiftModifier> Pool = new[]
    {
        new RiftModifier("field_medic",  "Field Medic",  "Restore HP to full.",                              "heal",        false),
        new RiftModifier("berserker",    "Berserker",    "+50% Power, but −1 max HP.",                        "risk",        false),
        new RiftModifier("ironclad",     "Ironclad",     "+2 max HP (and heal 2 now).",                       "defense",     true),
        new RiftModifier("keen_edge",    "Keen Edge",    "+15% Crit Chance.",                                 "offense",     true),
        new RiftModifier("cruelty",      "Cruelty",      "+50% Crit Damage.",                                 "offense",     true),
        new RiftModifier("twin_serve",   "Twin Serve",   "+1 ball for the rest of the rift.",                 "tempo",       true),
        new RiftModifier("prospector",   "Prospector",   "+30% rift end-reward (stacks each time taken).",    "economy",     true),
        new RiftModifier("wide_gait",    "Wide Gait",    "+25% paddle width.",                                "control",     true),
        new RiftModifier("snowball",     "Snowball",     "+5% Power for every rift level already cleared.",   "scaling",     false),
        new RiftModifier("cursed_bounty","Cursed Bounty","+40% reward, but +1 enemy emitter each level.",     "risk_reward", true),
    };

    public static RiftModifier? Get(string id) => Pool.FirstOrDefault(m => m.Id == id);

    /// <summary>Seeded 1-of-3 offer after a rift level. Non-stackable modifiers already taken are excluded so a
    /// choice is always meaningful; if the pool runs thin it backfills from the full list.</summary>
    public static List<RiftModifier> Offer(int seed, IEnumerable<string> alreadyTaken)
    {
        var taken = new HashSet<string>(alreadyTaken);
        var avail = Pool.Where(m => m.Stackable || !taken.Contains(m.Id)).ToList();
        if (avail.Count < 3) avail = Pool.ToList(); // fallback: allow repeats if everything non-stackable is gone
        var rng = new Rng(seed);
        var picks = new List<RiftModifier>();
        var bag = new List<RiftModifier>(avail);
        for (int i = 0; i < 3 && bag.Count > 0; i++)
        {
            int idx = rng.Range(bag.Count);
            picks.Add(bag[idx]);
            bag.RemoveAt(idx);
        }
        return picks;
    }

    /// <summary>Apply a picked modifier's IMMEDIATE effects to the run state (heal / max-HP / reward / difficulty),
    /// and record persistent ones in <see cref="DungeonRun.RiftModifiers"/>. Returns false if the id is unknown.</summary>
    public static bool Pick(DungeonRun run, string id, int heroMaxHp)
    {
        var mod = Get(id);
        if (mod is null) return false;
        // Establish the run's HP pool basis on first touch.
        if (run.RiftMaxHp <= 0) run.RiftMaxHp = heroMaxHp > 0 ? heroMaxHp : System.Math.Max(1, run.Hp);

        switch (id)
        {
            case "field_medic": run.Hp = run.RiftMaxHp; break;                       // heal to full
            case "berserker":   run.RiftMaxHp = System.Math.Max(1, run.RiftMaxHp - 1);
                                run.Hp = System.Math.Min(run.Hp, run.RiftMaxHp); break; // −1 max HP (Power applied in-game)
            case "ironclad":    run.RiftMaxHp += 2; run.Hp += 2; break;              // +2 max HP, heal 2 now
            case "prospector":  run.RewardMult += 0.30; break;
            case "cursed_bounty": run.RewardMult += 0.40; run.ExtraEmitters += 1; break;
            // keen_edge / cruelty / twin_serve / wide_gait / snowball: pure in-game stat effects (ApplyToGame).
        }
        run.RiftModifiers.Add(id); // persistent record (re-applied each level via ApplyToGame)
        return true;
    }

    /// <summary>Re-apply the run's persistent §8 modifiers to a fresh GameInstance at the start of each rift
    /// level. Stat picks stack; the run's HP pool + spare balls are carried by the caller (GameInitializer).</summary>
    public static void ApplyToGame(DungeonRun run, GameInstance game)
    {
        if (!run.IsRift || run.RiftModifiers.Count == 0) return;

        // Power multiplier (Berserker ×1.5 each; Snowball +5% per level already cleared).
        double powerMult = 1.0;
        foreach (var id in run.RiftModifiers)
        {
            if (id == "berserker") powerMult *= 1.5;
            if (id == "snowball")  powerMult *= 1.0 + 0.05 * run.FloorIndex;
        }
        if (System.Math.Abs(powerMult - 1.0) > 1e-6)
        {
            int basePower = game.StatPower > 0 ? game.StatPower : game.Config.BallDamage;
            game.StatPower = System.Math.Max(1, (int)System.Math.Round(basePower * powerMult));
        }

        // Crit picks (additive, stack), capped by SetCrit.
        double critChance = game.CritChance, critDamage = game.CritDamage;
        foreach (var id in run.RiftModifiers)
        {
            if (id == "keen_edge") critChance += 0.15;
            if (id == "cruelty")   critDamage += 0.50;
        }
        game.SetCrit(critChance, critDamage);

        // Twin Serve: +1 extra serve per pick. Wide Gait: +25% paddle width per pick.
        int twins = run.RiftModifiers.Count(id => id == "twin_serve");
        if (twins > 0) game.StatMultiball += twins;
        int wides = run.RiftModifiers.Count(id => id == "wide_gait");
        if (wides > 0) game.Paddle.Width *= System.Math.Pow(1.25, wides);

        // §7: the rift's single ball pool carries across levels (set here; HP is carried by GameInitializer).
        if (run.SpareBalls > 0) game.SpareBalls = run.SpareBalls;

        // §8 Cursed Bounty: force N normal blocks to also fire hazards (the modifier's downside) — +1 per pick.
        if (run.ExtraEmitters > 0)
        {
            int set = 0;
            foreach (var blk in game.Blocks)
            {
                if (set >= run.ExtraEmitters) break;
                if (blk.Dead || blk.Indestructible || blk.Boss || blk.Emitter) continue;
                blk.ForcedEmitter = true; set++;
            }
            if (set > 0) game.MarkBlocksDirty();
        }
    }

    /// <summary>Depth milestones (owner 2026-06-16): the reward steps UP each time the player passes one of
    /// these depths — surfaced in the HUD and on the end card so the run-further incentive is visible.</summary>
    public static readonly int[] Milestones = { 3, 5, 7, 10 };

    /// <summary>Multiplier from the milestones reached at the given depth: +35% per milestone passed
    /// (×1.35 at 3, ×1.70 at 5, ×2.05 at 7, ×2.40 at 10) on top of the accelerating base curve.</summary>
    public static double MilestoneMult(int levelsCleared)
        => 1.0 + 0.35 * Milestones.Count(m => levelsCleared >= m);

    /// <summary>The next depth at which the reward steps up (for the HUD "next bump at floor N"), or 0 once past 10.</summary>
    public static int NextMilestone(int levelsCleared)
        => Milestones.FirstOrDefault(m => m > levelsCleared); // 0 when none remain

    /// <summary>Depth-scaled end reward (§7 "depth = reward"): crystals for clearing <paramref name="levelsCleared"/>
    /// rift levels, ×milestone steps (3/5/7/10) ×<paramref name="rewardMult"/> (Prospector/Cursed Bounty).
    /// Reaching the last level is the jackpot.</summary>
    public static int DepthCrystals(int levelsCleared, int totalLevels, double rewardMult)
    {
        if (levelsCleared <= 0) return 0;
        int baseReward = 20 * levelsCleared + 10 * levelsCleared * levelsCleared; // accelerating curve
        double reward = baseReward * MilestoneMult(levelsCleared);                // milestone step-ups (3/5/7/10)
        if (levelsCleared >= totalLevels) reward *= 2;                            // jackpot for a full clear
        return (int)System.Math.Round(reward * rewardMult);
    }

    /// <summary>Hero Tokens scale with depth too (1 per 2 levels, +5 jackpot bonus on a full clear).</summary>
    public static int DepthTokens(int levelsCleared, int totalLevels)
        => levelsCleared <= 0 ? 0 : levelsCleared / 2 + (levelsCleared >= totalLevels ? 5 : 0);
}
