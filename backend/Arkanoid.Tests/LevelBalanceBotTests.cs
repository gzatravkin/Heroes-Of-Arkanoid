using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Arkanoid.Core.Blocks;
using Arkanoid.Core.Bonuses;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Meta;
using Arkanoid.Core.Relics;
using Arkanoid.Core.Sim;
using Arkanoid.Core.Spells;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// GENERAL-PURPOSE level-balance bot (not a regression gate): plays ANY level — boss fight or
/// regular level — with a REASONABLY GOOD player model: perfect ball catching and hazard
/// dodging, but a slightly randomized paddle-hit angle each catch (not deliberately steered)
/// and opportunistic random spell casts from whatever loadout you give it.
///
/// This is a tuning tool, not a "can a perfect player beat it" verdict: it reports win rate,
/// survival time, damage dealt PER SPELL (whatever spells are in the loadout, generically —
/// not hardcoded to one class's kit), and hazard-dodge difficulty across several RNG seeds so
/// the numbers can drive balance changes to ANY level, boss or not.
///
/// Uses the REAL production block/relic/character catalogs. Level id, character, loadout, and
/// SimConfig are all parameters to RunScenario, so this is reusable across the whole campaign —
/// see .claude/skills/level-balance-bot/SKILL.md.
/// </summary>
public class LevelBalanceBotTests
{
    private readonly ITestOutputHelper _out;
    public LevelBalanceBotTests(ITestOutputHelper output) => _out = output;

    private static string FindConfigRoot()
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

    // Multiple game-RNG seeds (attack-pattern/rain/spread/enemy rolls all draw from this) so a
    // single lucky/unlucky pattern draw can't stand in for "is this level balanced."
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

    // Example scenario: the hell demon boss fight with a fresh fire_mage, the case that prompted
    // this tool (2026-07-01, reported "completely off and unpassable"). Copy this test method's
    // body to point RunScenario at a different level/character/loadout — see the skill doc.
    [Fact]
    public void Bot_PlaysHellBoss_ReportsFullStatistics()
    {
        var configRoot = FindConfigRoot();
        var blockCat  = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
        var relicCat  = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
        var bonusPath = Path.Combine(configRoot, "bonuses.json");
        var bonusCat  = File.Exists(bonusPath) ? BonusCatalog.FromFile(bonusPath) : null;
        var charCat   = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));

        // Matches the loadout a fresh fire_mage actually plays with (verified live 2026-07-01):
        // signature Ignite + drafted Conflagration ("fireball") + Fire Wall.
        var bots = RunScenario("CURRENT COMMITTED TUNING", configRoot, blockCat, relicCat, bonusCat, charCat,
            "hell-boss", "fire_mage", new[] { "ignite", "fireball", "firewall" }, SimConfig.Default, halveBossHp: false);

        _out.WriteLine("Full detail for seed 1:");
        bots[0].PrintReport(_out);
        PrintAggregateSummary(bots, "CURRENT COMMITTED TUNING");

        foreach (var bot in bots)
            Assert.True(bot.FinalPhase is GamePhase.Won or GamePhase.Lost,
                $"seed {bot.Seed}: bot never reached a terminal phase within {bot.SimSeconds:F1}s of sim time " +
                "(possible soft-lock — not a win/loss verdict, an infra problem).");
    }

    // Whole-biome sweep (2026-07-01): every hell level, every class's starting kit, plus a
    // generated Continuous Rift, to get a general balance read rather than one boss/one class.
    private static readonly string[] SweepClasses = { "fire_mage", "paladin", "engineer", "necromancer" };
    private static readonly int[] SweepSeeds = { 1, 2, 3, 4, 5, 6 };
    private static readonly int[] RiftSeeds  = { 1, 2, 3 };

    // Engineer exception (2026-07-01, researched against docs/04 §"Engineer" before changing
    // anything): its config/characters.json "starting" kit (overload/magnet/radiation) has NO
    // burst damage at all — but that's not an oversight to patch with a numeric buff. The design
    // doc's Engineer identity leans on relics/itemization for damage (passive: "+1 relic slot;
    // relics cheaper"), and separately, config/characters.json's fixed 3-spell "starting" kits are
    // themselves a KNOWN DRIFT from the real acquisition model (signature locked + spells drafted
    // from a global pool — see the 2026-06-14 economy-rework doc referenced in CLAUDE.md). Its
    // real burst tools (Lightning, Rocket) exist in the spell pool but aren't in "starting" at all.
    // Testing with them here matches how the class is actually meant to be equipped, instead of
    // reinforcing the already-flagged fixed-kit drift with a bigger Overload number.
    // Fire Mage exception (2026-07-03, same reasoning as Engineer above): its "starting" kit is
    // ["ignite","fireball","phoenix"] — all three are pure offense, leaving the class with ZERO
    // anti-hazard tool. Its actual anti-hazard tool, Fire Wall (redesigned this session specifically
    // to reflect the ball / destroy crossing hazards / ignite on cross), exists in the spell pool but
    // isn't in "starting" — the whole-campaign sweep's uniform fire_mage struggles at caverns-boss,
    // heaven-boss, and hell-10 traced back to this: fire_mage was the one class with no way to ever
    // acquire the reactive-defense play the bot (and a real player) would otherwise use. Swap Phoenix
    // for Fire Wall — matches the live kit already used in the original hell-boss investigation
    // (Bot_PlaysHellBoss_ReportsFullStatistics), not a buff.
    private static string[] LoadoutFor(string classId, CharacterCatalog charCat)
    {
        if (classId == "engineer") return new[] { "overload", "lightning", "rocket" };
        if (classId == "fire_mage") return new[] { "ignite", "fireball", "firewall" };
        charCat.TryGet(classId, out var def);
        var starting = def!.Starting.Count > 0 ? def.Starting : def.Spells.Select(s => s.Id).Take(3).ToList();
        return starting.ToArray();
    }

    [Fact]
    public void Bot_PlaysWholeInfernoAndRift_GeneratesHtmlReport()
    {
        var configRoot = FindConfigRoot();
        var blockCat  = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
        var relicCat  = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
        var bonusPath = Path.Combine(configRoot, "bonuses.json");
        var bonusCat  = File.Exists(bonusPath) ? BonusCatalog.FromFile(bonusPath) : null;
        var charCat   = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));

        string[] LoadoutFor(string classId) => LevelBalanceBotTests.LoadoutFor(classId, charCat);

        var hellLevels = Enumerable.Range(1, 11).Select(i => $"hell-{i}").Append("hell-boss").ToList();

        var sweep = new List<(string Level, string Class, List<LevelBot> Bots)>();
        foreach (var levelId in hellLevels)
            foreach (var classId in SweepClasses)
            {
                var bots = RunScenario($"{levelId}/{classId}", configRoot, blockCat, relicCat, bonusCat, charCat,
                    levelId, classId, LoadoutFor(classId), SimConfig.Default, halveBossHp: false, seeds: SweepSeeds);
                sweep.Add((levelId, classId, bots));
                _out.WriteLine($"{levelId,-12} {classId,-12} win={100.0*bots.Count(b=>b.FinalPhase==GamePhase.Won)/bots.Count:F0}%  " +
                                $"avgTime={bots.Average(b=>b.SimSeconds):F1}s  avgCleared={bots.Average(b=>b.BlocksClearedPct):F0}%");
            }

        var riftResults = new List<(string Class, List<LevelBot> Bots)>();
        foreach (var classId in SweepClasses)
        {
            var bots = RunRiftScenario(configRoot, blockCat, relicCat, bonusCat, charCat, "hell", classId, LoadoutFor(classId), RiftSeeds);
            riftResults.Add((classId, bots));
            _out.WriteLine($"RIFT         {classId,-12} win={100.0*bots.Count(b=>b.FinalPhase==GamePhase.Won)/bots.Count:F0}%  " +
                            $"avgFloors={bots.Average(b=>b.FloorsCleared):F1}/{bots[0].FloorsTotal}  avgTime={bots.Average(b=>b.SimSeconds):F0}s");
        }

        // Rift is meant to open only after a full campaign clear (RiftService), so a real attempt
        // is never made at base stats. Re-test at a representative "cleared the campaign, has put
        // some points in" progression (hero level 15, 2 stars — mid-progression, not maxed) to see
        // whether the earlier 0-33% win rate reflects a real balance problem or just testing outside
        // the mode's intended power curve.
        const int ProgressedLevel = 15, ProgressedStars = 2;
        var riftProgressedResults = new List<(string Class, List<LevelBot> Bots)>();
        foreach (var classId in SweepClasses)
        {
            var bots = RunRiftScenario(configRoot, blockCat, relicCat, bonusCat, charCat, "hell", classId, LoadoutFor(classId), RiftSeeds,
                progressedLevel: ProgressedLevel, progressedStars: ProgressedStars);
            riftProgressedResults.Add((classId, bots));
            _out.WriteLine($"RIFT(Lv{ProgressedLevel}*{ProgressedStars}) {classId,-12} win={100.0*bots.Count(b=>b.FinalPhase==GamePhase.Won)/bots.Count:F0}%  " +
                            $"avgFloors={bots.Average(b=>b.FloorsCleared):F1}/{bots[0].FloorsTotal}  avgTime={bots.Average(b=>b.SimSeconds):F0}s");
        }

        var repoRoot = Directory.GetParent(configRoot)!.FullName;
        var outDir   = Path.Combine(repoRoot, "docs", "balance-reports");
        Directory.CreateDirectory(outDir);
        var outPath  = Path.Combine(outDir, "2026-07-01-inferno-balance.html");
        File.WriteAllText(outPath, BuildHtmlReport(sweep, riftResults, riftProgressedResults));
        _out.WriteLine($"\nHTML report written to: {outPath}");

        // This sweep is a data-collection tool, not a regression gate — a run hitting the time cap
        // without resolving is a real (if rare) outcome of the random-catch-angle bot model (no
        // deliberate aim means occasionally a stray last block just doesn't get visited in time),
        // not necessarily an engine soft-lock. Log it prominently and count it as a non-win in the
        // report (FinalPhase stays "Playing", which Cell()/Grade() already treat as not-a-win)
        // rather than failing the whole sweep.
        void WarnIfUnresolved(string label, List<LevelBot> bots)
        {
            foreach (var bot in bots)
                if (bot.FinalPhase is not (GamePhase.Won or GamePhase.Lost))
                    _out.WriteLine($"WARNING: {label} seed {bot.Seed} never reached a terminal phase after " +
                        $"{bot.SimSeconds:F0}s (cleared {bot.BlocksClearedPct:F0}%, boss {bot.BossStartHp}->{bot.BossEndHp}) " +
                        "— treated as a non-win, not asserted as a failure.");
        }
        foreach (var (level, cls, bots) in sweep) WarnIfUnresolved($"{level}/{cls}", bots);
        foreach (var (cls, bots) in riftResults) WarnIfUnresolved($"RIFT/{cls}", bots);
        foreach (var (cls, bots) in riftProgressedResults) WarnIfUnresolved($"RIFT(progressed)/{cls}", bots);
    }

    // Whole-campaign sweep (2026-07-03): the inferno sweep above only ever covered hell — caverns,
    // village, and heaven have never had this per-level x per-class pass run against them. Answers
    // "is balance actually fine everywhere" with data instead of assuming the hell fixes generalize.
    private static readonly string[] AllBiomes = { "caverns", "village", "heaven", "hell" };

    [Fact]
    public void Bot_PlaysWholeCampaign_GeneratesHtmlReport()
    {
        var configRoot = FindConfigRoot();
        var blockCat  = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
        var relicCat  = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
        var bonusPath = Path.Combine(configRoot, "bonuses.json");
        var bonusCat  = File.Exists(bonusPath) ? BonusCatalog.FromFile(bonusPath) : null;
        var charCat   = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));

        var sweep = new List<(string Level, string Class, List<LevelBot> Bots)>();
        foreach (var biome in AllBiomes)
        {
            var levels = Enumerable.Range(1, 11).Select(i => $"{biome}-{i}").Append($"{biome}-boss").ToList();
            foreach (var levelId in levels)
                foreach (var classId in SweepClasses)
                {
                    var bots = RunScenario($"{levelId}/{classId}", configRoot, blockCat, relicCat, bonusCat, charCat,
                        levelId, classId, LoadoutFor(classId, charCat), SimConfig.Default, halveBossHp: false, seeds: SweepSeeds);
                    sweep.Add((levelId, classId, bots));
                    _out.WriteLine($"{levelId,-14} {classId,-12} win={100.0*bots.Count(b=>b.FinalPhase==GamePhase.Won)/bots.Count:F0}%  " +
                                    $"score={bots.Average(b=>b.LevelScore):F0}  avgTime={bots.Average(b=>b.SimSeconds):F1}s  avgCleared={bots.Average(b=>b.BlocksClearedPct):F0}%");
                }
        }

        var repoRoot = Directory.GetParent(configRoot)!.FullName;
        var outDir   = Path.Combine(repoRoot, "docs", "balance-reports");
        Directory.CreateDirectory(outDir);
        var outPath  = Path.Combine(outDir, "2026-07-03-whole-campaign-balance.html");
        File.WriteAllText(outPath, BuildHtmlReport(sweep, new List<(string, List<LevelBot>)>(), new List<(string, List<LevelBot>)>()));
        _out.WriteLine($"\nHTML report written to: {outPath}");

        foreach (var (level, cls, bots) in sweep)
            foreach (var bot in bots)
                if (bot.FinalPhase is not (GamePhase.Won or GamePhase.Lost))
                    _out.WriteLine($"WARNING: {level}/{cls} seed {bot.Seed} never reached a terminal phase after " +
                        $"{bot.SimSeconds:F0}s (cleared {bot.BlocksClearedPct:F0}%, boss {bot.BossStartHp}->{bot.BossEndHp}) " +
                        "— treated as a non-win, not asserted as a failure.");
    }

    private static string BuildHtmlReport(
        List<(string Level, string Class, List<LevelBot> Bots)> sweep,
        List<(string Class, List<LevelBot> Bots)> rift,
        List<(string Class, List<LevelBot> Bots)> riftProgressed)
    {
        var levels  = sweep.Select(r => r.Level).Distinct().ToList();
        var classes = sweep.Select(r => r.Class).Distinct().ToList();

        static string Grade(double winPct) => winPct >= 65 ? "good" : winPct >= 35 ? "ok" : "bad";

        string Cell(List<LevelBot> bots)
        {
            int wins = bots.Count(b => b.FinalPhase == GamePhase.Won);
            double winPct = 100.0 * wins / bots.Count;
            double avgTime = bots.Average(b => b.SimSeconds);
            double avgCleared = bots.Average(b => b.BlocksClearedPct);
            double avgHpLost = bots.Average(b => b.HpLost);
            double avgBallsDropped = bots.Average(b => b.BallsDropped);
            double avgScore = bots.Average(b => b.LevelScore);
            int infeasible = bots.Sum(b => b.InfeasibleDodgeCount);
            int undodge = bots.Sum(b => b.Undodgeable.Count);
            var extra = new System.Text.StringBuilder();
            if (undodge > 0) extra.Append($"<br><span class='danger'>{undodge} UNDODGEABLE</span>");
            else if (infeasible > 0) extra.Append($"<br><span class='warn'>{infeasible} infeasible dodges</span>");
            return $"<td class='{Grade(winPct)}'><b>{winPct:F0}%</b> win &middot; score {avgScore:F0}<br>{avgTime:F0}s &middot; {avgCleared:F0}% cleared<br>{avgHpLost:F1} HP lost &middot; {avgBallsDropped:F1} balls lost{extra}</td>";
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>Inferno Balance Report</title><style>");
        sb.Append(@"
body { font-family: -apple-system, 'Segoe UI', sans-serif; background:#15100b; color:#e8dcc8; padding:24px; }
h1 { color:#ff9040; margin-bottom:4px; }
h2 { color:#ffd27a; margin-top:36px; border-bottom:1px solid #4a3a28; padding-bottom:6px; }
p.sub { color:#a89478; margin-top:0; }
table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
th, td { border: 1px solid #4a3a28; padding: 8px 10px; text-align: center; font-size: 12.5px; line-height:1.5; }
th { background: #2a1f14; color: #ffd27a; position: sticky; top:0; }
th.rowhead, td.rowhead { text-align:left; background:#241a10; color:#ffd27a; font-weight:bold; }
td.good { background: #1c3320; }
td.ok   { background: #3a3318; }
td.bad  { background: #3a1c1c; }
.danger { color: #ff6b6b; font-weight:bold; }
.warn   { color: #ffb457; }
ul.findings li { margin-bottom: 6px; }
");
        sb.Append("</style></head><body>");
        sb.Append("<h1>Inferno Biome Balance Report</h1>");
        sb.Append("<p class='sub'>level-balance-bot &middot; reasonable-player model (ideal targeting capped at " +
                   $"{LevelBot.MaxPaddleSpeed:F0}px/s real paddle speed — not a teleport, so it can actually miss — random catch angle, random spell casts) &middot; " +
                   $"{SweepSeeds.Length} seeds/level &middot; generated 2026-07-01</p>");

        sb.Append("<h2>Per-level &times; per-class</h2><table><tr><th class='rowhead'>Level</th>");
        foreach (var c in classes) sb.Append($"<th>{c}</th>");
        sb.Append("</tr>");
        foreach (var levelId in levels)
        {
            sb.Append($"<tr><td class='rowhead'>{levelId}</td>");
            foreach (var classId in classes)
            {
                var bots = sweep.First(r => r.Level == levelId && r.Class == classId).Bots;
                sb.Append(Cell(bots));
            }
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        void RiftTable(string heading, List<(string Class, List<LevelBot> Bots)> data)
        {
            if (data.Count == 0) return;
            sb.Append($"<h2>{heading}</h2><table><tr><th class='rowhead'>Class</th><th>Win %</th><th>Avg floors cleared</th><th>Avg survival</th><th>Avg HP lost</th><th>Infeasible dodges</th></tr>");
            foreach (var (classId, bots) in data)
            {
                int wins = bots.Count(b => b.FinalPhase == GamePhase.Won);
                double winPct = 100.0 * wins / bots.Count;
                sb.Append($"<tr><td class='rowhead'>{classId}</td><td class='{Grade(winPct)}'><b>{winPct:F0}%</b></td>" +
                           $"<td>{bots.Average(b => b.FloorsCleared):F1} / {bots[0].FloorsTotal}</td>" +
                           $"<td>{bots.Average(b => b.SimSeconds):F0}s</td>" +
                           $"<td>{bots.Average(b => b.HpLost):F1}</td>" +
                           $"<td>{bots.Sum(b => b.InfeasibleDodgeCount)}</td></tr>");
            }
            sb.Append("</table>");
        }
        RiftTable("Continuous Rift — base stats (10 hell floors + hell-boss)", rift);
        RiftTable("Continuous Rift — progressed stats (hero level 15, 2★ — a realistic post-campaign attempt)", riftProgressed);

        // Auto-flagged worst offenders (win rate under 35%, excluding boss levels which are already known-hard).
        var worst = sweep.Where(r => !r.Level.EndsWith("-boss"))
            .Select(r => (r.Level, r.Class, WinPct: 100.0 * r.Bots.Count(b => b.FinalPhase == GamePhase.Won) / r.Bots.Count))
            .Where(r => r.WinPct < 35)
            .OrderBy(r => r.WinPct)
            .ToList();
        sb.Append("<h2>Auto-flagged: regular levels under 35% win rate</h2>");
        if (worst.Count == 0)
            sb.Append("<p>None — every non-boss level cleared at least 35% of the time.</p>");
        else
        {
            sb.Append("<ul class='findings'>");
            foreach (var w in worst)
                sb.Append($"<li><b>{w.Level}</b> as <b>{w.Class}</b>: {w.WinPct:F0}% win rate</li>");
            sb.Append("</ul>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    // Cross-biome spell-coverage sweep (2026-07-02): every spell in the game, not just each class's
    // fixed 3-slot "starting" kit. Each class's loadout here is its FULL Spells pool (config's fixed
    // 3-slot cap doesn't apply to this test harness — CastSlot just indexes whatever list SetLoadout
    // was given), so the bot's random-cast policy gets a chance to exercise every spell over enough
    // attempts. The two class-less "neutral" spells (Recall, Slow Time) are appended to every class's
    // loadout too, since they don't have a home class of their own.
    private static readonly string[] RandomLevelSample = { "caverns-5", "caverns-9", "village-10", "heaven-6", "hell-10" };
    private static readonly int[] SpellSweepSeeds = { 1, 2, 3, 4, 5, 6 };

    [Fact]
    public void Bot_TestsEverySpell_AcrossFiveRandomLevels()
    {
        var configRoot = FindConfigRoot();
        var blockCat  = BlockCatalog.FromFile(Path.Combine(configRoot, "blocks.json"));
        var relicCat  = RelicCatalog.FromFile(Path.Combine(configRoot, "relics.json"));
        var bonusPath = Path.Combine(configRoot, "bonuses.json");
        var bonusCat  = File.Exists(bonusPath) ? BonusCatalog.FromFile(bonusPath) : null;
        var charCat   = CharacterCatalog.FromFile(Path.Combine(configRoot, "characters.json"));

        var allBotsByClass = new Dictionary<string, List<LevelBot>>();
        foreach (var classId in SweepClasses)
        {
            charCat.TryGet(classId, out var def);
            var loadout = def!.Spells.Select(s => s.Id).Append("recall").Append("slowtime").ToArray();

            var classBots = new List<LevelBot>();
            foreach (var levelId in RandomLevelSample)
            {
                var bots = RunScenario($"{levelId}/{classId}(all-spells)", configRoot, blockCat, relicCat, bonusCat, charCat,
                    levelId, classId, loadout, SimConfig.Default, halveBossHp: false, seeds: SpellSweepSeeds);
                classBots.AddRange(bots);
            }
            allBotsByClass[classId] = classBots;

            var perSpell = classBots.SelectMany(b => b.Casts).GroupBy(c => c.Id)
                .Select(g => (Id: g.Key, Attempted: g.Count(), Landed: g.Count(c => c.Consumed)))
                .OrderByDescending(x => x.Attempted);
            int classWins = classBots.Count(b => b.FinalPhase == GamePhase.Won);
            _out.WriteLine($"--- {classId} ({loadout.Length} spells tested across {RandomLevelSample.Length} levels x {SpellSweepSeeds.Length} seeds) --- " +
                            $"win={100.0*classWins/classBots.Count:F0}%  avgLevelScore={classBots.Average(b => b.LevelScore):F0}");
            foreach (var s in perSpell)
            {
                var usedBots = classBots.Where(b => b.Casts.Any(c => c.Id == s.Id && c.Consumed)).ToList();
                var restBots = classBots.Where(b => !usedBots.Contains(b)).ToList();
                string usedStr = usedBots.Count > 0 ? $"{usedBots.Average(b => b.LevelScore):F0}(n={usedBots.Count})" : "—";
                string restStr = restBots.Count > 0 ? $"{restBots.Average(b => b.LevelScore):F0}(n={restBots.Count})" : "—";
                _out.WriteLine($"  {s.Id,-12} attempted={s.Attempted,4}  landed={s.Landed,4} ({100.0*s.Landed/s.Attempted:F0}%)  " +
                                $"scoreWhenCast={usedStr,-14} scoreOtherwise={restStr,-14}");
            }
            var neverAttempted = loadout.Except(classBots.SelectMany(b => b.Casts).Select(c => c.Id).Distinct());
            foreach (var missing in neverAttempted)
                _out.WriteLine($"  {missing,-12} NEVER ATTEMPTED across all runs — check mana cost vs. regen, or a candidate-filter bug");
        }

        var repoRoot = Directory.GetParent(configRoot)!.FullName;
        var outDir   = Path.Combine(repoRoot, "docs", "balance-reports");
        Directory.CreateDirectory(outDir);
        var outPath  = Path.Combine(outDir, "2026-07-03-all-spells-report.html");
        File.WriteAllText(outPath, BuildSpellReport(allBotsByClass, charCat));
        _out.WriteLine($"\nHTML report written to: {outPath}");
    }

    private static string BuildSpellReport(Dictionary<string, List<LevelBot>> botsByClass, CharacterCatalog charCat)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset='utf-8'><title>All-Spells Coverage Report</title><style>");
        sb.Append(@"
body { font-family: -apple-system, 'Segoe UI', sans-serif; background:#15100b; color:#e8dcc8; padding:24px; }
h1 { color:#ff9040; margin-bottom:4px; }
h2 { color:#ffd27a; margin-top:36px; border-bottom:1px solid #4a3a28; padding-bottom:6px; }
p.sub { color:#a89478; margin-top:0; }
table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }
th, td { border: 1px solid #4a3a28; padding: 7px 10px; text-align: center; font-size: 13px; }
th { background: #2a1f14; color: #ffd27a; }
td.spellname { text-align:left; font-weight:bold; background:#241a10; color:#ffd27a; }
td.good { background: #1c3320; }
td.ok   { background: #3a3318; }
td.bad  { background: #3a1c1c; }
td.dim  { color:#7a6a56; }
");
        sb.Append("</style></head><body>");
        sb.Append("<h1>All-Spells Coverage Report</h1>");
        sb.Append($"<p class='sub'>level-balance-bot &middot; every spell in each class's full pool (+ Recall/Slow Time) &middot; " +
                   $"levels: {string.Join(", ", RandomLevelSample)} &middot; {SpellSweepSeeds.Length} seeds/level &middot; generated 2026-07-03</p>");
        sb.Append("<p class='sub'><b>Level Score</b> replaces per-spell damage, which was only ever precisely measurable for a couple " +
                   "of instant-effect spells — every other spell showed a meaningless \"—\". Score = seconds to clear the level, plus " +
                   $"{LevelBot.SecondsPerLostHpOrBall:F0}s for every HP/ball lost along the way; a run that lost or timed out scores a flat " +
                   $"{LevelBot.FailScore:F0} regardless of how far it got — no partial credit. <b>Lower is always better.</b> " +
                   "\"Avg score when cast\" is the average score of runs where the bot landed that spell at least once; \"avg score otherwise\" " +
                   "is the same average for runs that never landed it. This is a CORRELATION across the sweep's random casting, not a " +
                   "controlled A/B — a reactive/defensive spell (Shield, Recall) tends to get cast more precisely when a run is already going " +
                   "badly, which can make it look artificially worse than it is. Read it as a diagnostic pointer, not a verdict — especially " +
                   "for spells with a small n.</p>");

        foreach (var (classId, bots) in botsByClass)
        {
            charCat.TryGet(classId, out var def);
            var spellNames = def?.Spells.ToDictionary(s => s.Id, s => s.Name) ?? new Dictionary<string, string>();

            int wins = bots.Count(b => b.FinalPhase == GamePhase.Won);
            sb.Append($"<h2>{classId}</h2><p class='sub'>{wins}/{bots.Count} runs won &middot; avg Level Score across ALL runs: <b>{bots.Average(b => b.LevelScore):F0}</b></p>");
            sb.Append("<table><tr><th class='spellname'>Spell</th><th>Attempted</th><th>Landed</th><th>Land rate</th><th>Avg score when cast</th><th>Avg score otherwise</th></tr>");
            var casts = bots.SelectMany(b => b.Casts).ToList();
            var byId = casts.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var id in byId.Keys.OrderByDescending(id => byId[id].Count))
            {
                var list = byId[id];
                int attempted = list.Count, landed = list.Count(c => c.Consumed);
                double rate = 100.0 * landed / attempted;
                string rateGrade = attempted == 0 ? "bad" : rate >= 80 ? "good" : rate >= 40 ? "ok" : "bad";

                var usedBots = bots.Where(b => b.Casts.Any(c => c.Id == id && c.Consumed)).ToList();
                var restBots = bots.Where(b => !usedBots.Contains(b)).ToList();
                string name = spellNames.TryGetValue(id, out var n) ? n : id;

                string usedCell = usedBots.Count > 0
                    ? $"{usedBots.Average(b => b.LevelScore):F0} <span class='dim'>(n={usedBots.Count})</span>"
                    : "<span class='dim'>—</span>";
                string restCell = restBots.Count > 0
                    ? $"{restBots.Average(b => b.LevelScore):F0} <span class='dim'>(n={restBots.Count})</span>"
                    : "<span class='dim'>— (cast in every run)</span>";

                sb.Append($"<tr><td class='spellname'>{name} <span class='dim'>({id})</span></td><td>{attempted}</td><td>{landed}</td>" +
                           $"<td class='{rateGrade}'><b>{rate:F0}%</b></td><td>{usedCell}</td><td>{restCell}</td></tr>");
            }
            sb.Append("</table>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>Reusable balance-test harness: runs the reasonable-player bot across `seeds`
    /// (defaults to the standard 8) against ANY level/character/loadout/SimConfig (and optionally
    /// halved boss-tile HP, only relevant when the level has boss blocks).
    /// See .claude/skills/level-balance-bot/SKILL.md.</summary>
    private List<LevelBot> RunScenario(string title, string configRoot, BlockCatalog blockCat,
        RelicCatalog relicCat, BonusCatalog? bonusCat, CharacterCatalog charCat,
        string levelId, string characterId, string[] loadout, SimConfig cfg, bool halveBossHp,
        int[]? seeds = null)
    {
        var bots = new List<LevelBot>();
        foreach (var seed in seeds ?? Seeds)
        {
            // Fresh level load per run — GameInstance mutates block state in place.
            var level = LevelLoader.FromFile(Path.Combine(configRoot, "levels", $"{levelId}.json"), blockCat);
            var g = new GameInstance(level, cfg, seed: seed, relics: relicCat, bonuses: bonusCat, chars: charCat);
            g.SetCharacter(characterId);
            g.SetLoadout(loadout);

            if (halveBossHp)
                foreach (var b in g.Blocks.Where(b => b.Boss)) { b.Hp = Math.Max(1, b.MaxHp / 2); b.MaxHp = b.Hp; }

            var bot = new LevelBot(g, gameSeed: seed, rngSeed: 12345 + seed);
            bot.Run(maxSimSeconds: 600);
            bots.Add(bot);
        }
        return bots;
    }

    /// <summary>Builds and runs a Continuous Rift: `floorCount` biome levels (seed-shuffled from the
    /// pool, cycling if the pool is smaller) ending at `{biome}-boss`, stacked into ONE GameInstance
    /// via LevelLoader.FromRiftFloorFiles — mirrors RiftService.GenerateRift's real selection shape.
    /// Mid-rift §8 modifier drafts are auto-picked at random by LevelBot.Run.</summary>
    private List<LevelBot> RunRiftScenario(string configRoot, BlockCatalog blockCat, RelicCatalog relicCat,
        BonusCatalog? bonusCat, CharacterCatalog charCat, string biome, string characterId, string[] loadout,
        int[] seeds, int floorCount = 10, int? progressedLevel = null, int? progressedStars = null)
    {
        var pool = Enumerable.Range(1, 11).Select(i => $"{biome}-{i}").ToList();
        var bots = new List<LevelBot>();
        foreach (var seed in seeds)
        {
            var shuffleRng = new Random(90_000 + seed);
            var shuffled = pool.OrderBy(_ => shuffleRng.Next()).ToList();
            var floorIds = new List<string>();
            for (int i = 0; i < floorCount; i++) floorIds.Add(shuffled[i % shuffled.Count]);
            floorIds.Add($"{biome}-boss");
            var floorPaths = floorIds.Select(id => Path.Combine(configRoot, "levels", $"{id}.json"));

            var level = LevelLoader.FromRiftFloorFiles(floorPaths, blockCat, SimConfig.Default);
            var g = new GameInstance(level, SimConfig.Default, seed: seed, relics: relicCat, bonuses: bonusCat, chars: charCat);
            g.SetRiftMode(true);
            g.SetCharacter(characterId);
            g.SetLoadout(loadout);

            if (progressedLevel.HasValue)
            {
                var stats = StatResolver.Resolve(characterId, progressedLevel.Value, progressedStars ?? 0);
                StatResolver.Apply(stats, g);
                g.SetPerks(StatResolver.PerksFor(characterId, progressedStars ?? 0));
            }

            var bot = new LevelBot(g, gameSeed: seed, rngSeed: 55_555 + seed) { FloorsTotal = floorIds.Count };
            bot.Run(maxSimSeconds: 1800); // long cap — up to 11 floors of survival + clearing
            bots.Add(bot);
        }
        return bots;
    }

    private void PrintAggregateSummary(List<LevelBot> bots, string title)
    {
        // Spell columns are derived from whatever was actually cast, so the table adapts to
        // any loadout automatically instead of hardcoding one class's kit.
        var spellIds = bots.SelectMany(b => b.SpellDamage.Keys).Distinct().OrderBy(x => x).ToList();

        _out.WriteLine("");
        _out.WriteLine($"================ {title} ================");
        _out.WriteLine($"{"seed",4}  {"outcome",-6}  {"time",6}  {"cleared",8}  {"boss hp",9}  {"hp lost",7}  " +
                        string.Join("  ", spellIds.Select(s => $"{("dmg:" + s),12}")) +
                        $"  {"dmg:ball",9}  {"max speed",11}  {"infeasible",10}  {"undodge",7}");
        foreach (var bot in bots)
        {
            _out.WriteLine($"{bot.Seed,4}  {bot.FinalPhase,-6}  {bot.SimSeconds,5:F1}s  {bot.BlocksClearedPct,6:F0}%  {bot.BossStartHp,3}->{bot.BossEndHp,-3}  " +
                            $"{bot.HpLost,7}  " +
                            string.Join("  ", spellIds.Select(s => $"{bot.SpellDamage.GetValueOrDefault(s),12}")) +
                            $"  {bot.BallCollisionDamage,9}  {bot.MaxRequiredSpeed,8:F0}px/s  {bot.InfeasibleDodgeCount,10}  {bot.Undodgeable.Count,7}");
        }
        int wins = bots.Count(b => b.FinalPhase == GamePhase.Won);
        _out.WriteLine("");
        _out.WriteLine($"Win rate:                 {wins}/{bots.Count} ({100.0 * wins / bots.Count:F0}%)");
        _out.WriteLine($"Avg Level Score:          {bots.Average(b => b.LevelScore):F0}  (lower is better; {LevelBot.FailScore:F0} = loss/timeout)");
        _out.WriteLine($"Avg survival time:        {bots.Average(b => b.SimSeconds):F1}s");
        _out.WriteLine($"Avg blocks cleared:       {bots.Average(b => b.BlocksClearedPct):F0}%");
        if (bots[0].BossStartHp > 0)
            _out.WriteLine($"Avg boss HP remaining:    {bots.Average(b => b.BossEndHp):F1} / {bots[0].BossStartHp}");
        _out.WriteLine("Total dmg by spell:       " + string.Join("  ", spellIds
            .Select(s => $"{s}: {bots.Sum(b => b.SpellDamage.GetValueOrDefault(s))}")) +
            $"  ball/board: {bots.Sum(b => b.BallCollisionDamage)}");
        _out.WriteLine($"Spell cast totals:        " + string.Join("  ", bots
            .SelectMany(b => b.Casts)
            .GroupBy(c => c.Id)
            .Select(g => $"{g.Key}: {g.Count(c => c.Consumed)}/{g.Count()} landed")));
        _out.WriteLine($"Total undodgeable waves:  {bots.Sum(b => b.Undodgeable.Count)} across all seeds " +
                        "(a wave where NO paddle position, anywhere on the board, avoided every simultaneous hazard)");
        _out.WriteLine($"Total infeasible dodges:  {bots.Sum(b => b.InfeasibleDodgeCount)} across all seeds " +
                        $"(a safe spot existed but required moving the paddle faster than {LevelBot.HumanSpeedThreshold:F0}px/s " +
                        "instantly — beyond any real finger/mouse reaction)");
        _out.WriteLine("==============================================================");
    }
}

internal sealed class LevelBot
{
    // A very generous ceiling for real human input: a fast phone-swipe flick. Any required dodge
    // speed above this means the sim's teleport-perfect paddle could technically reach safety, but
    // no real player's finger/mouse could — i.e. "geometrically dodgeable" is not "humanly dodgeable".
    // This stays a DIAGNOSTIC ceiling (see reqSpeed in Run) — it does not limit actual movement.
    public const double HumanSpeedThreshold = 2000.0;

    // The bot is "reasonably good, not superhuman" (2026-07-02): actual paddle movement is capped
    // at this sustained tracking speed — a fast but human-plausible flick, lower than
    // HumanSpeedThreshold's generous peak-flick ceiling since sustained tracking is slower than one
    // isolated flick. Below, the bot reaches its ideal target every tick like before; above, it falls
    // short and can genuinely miss — this is what makes HP-lost/balls-dropped a real per-level signal
    // instead of flatlining at 0 against a teleporting paddle that never fails a geometrically
    // possible dodge.
    public const double MaxPaddleSpeed = 1400.0;

    // How far off-center a catch lands, as a fraction of paddle half-width (0 = dead center,
    // 1 = edge). Regenerated randomly after every deflect — models a decent-but-not-robotic
    // player who doesn't consciously aim shots, rather than a min-maxed steering AI.
    private const double MaxRandomAngleFrac = 0.6;

    // Lava-spawner awareness (2026-07-03): Hell's LavaSystem only starts creeping a spawner AFTER
    // its first hit (hp < maxHp), then drains 1 HP every 3s once any crept lava reaches the danger
    // row — a DIRECT CombatSystem.DamagePlayer call with no Hazards-list entry at all, so it was
    // completely invisible to the dodge model (same blind-spot class as Fist Slam) AND literally
    // impossible to avoid by paddle positioning once it starts — the only counterplay is finishing
    // the spawner off. A real "reasonably good" player who's already chipped a lava spawner would
    // notice it's now spewing lava and focus subsequent shots on it rather than let it keep creeping.
    // This biases (not forces — still random magnitude/imprecise) the post-catch angle SIGN toward
    // whichever already-damaged-but-alive spawner is at the deepest row, same spirit as the random
    // catch angle elsewhere: a nudge toward sensible play, not pixel-perfect trajectory aiming.
    private double? FindLavaPriorityColX()
    {
        Block? best = null;
        int bestRow = -1;
        foreach (var b in _g.Blocks)
        {
            if (b.Dead || !b.LavaSpawner || b.Hp >= b.MaxHp) continue;
            if (best == null || b.Row > bestRow) { best = b; bestRow = b.Row; }
        }
        return best == null ? null : _g.Level.Grid.CellCenter(best.Col, best.Row).X;
    }

    // Reviver awareness (2026-07-03): Village's necromant blocks (ReviverSystem) resurrect every
    // same-layer block destroyed while they live — so clearing progress doesn't stick at all until
    // the matching necromant dies ("kill it or out-pace it"). A ball not deliberately aimed at the
    // reviver just spins its wheels killing blocks that come right back, which reads as "level stuck
    // at ~5% cleared" even though the ball is landing plenty of hits. Only the REGULAR-layer reviver
    // (BallPhases==false) is targetable here — a ghost reviver needs the ball phased via a portal
    // first, which is a deliberate multi-step routing puzzle this bot doesn't attempt to solve; that
    // half of a ghost-gated level is a real bot-coverage gap, not a balance verdict either way.
    private double? FindReviverPriorityColX()
    {
        var b = _g.Blocks.FirstOrDefault(x => !x.Dead && x.Reviver && !x.BallPhases);
        return b == null ? null : _g.Level.Grid.CellCenter(b.Col, b.Row).X;
    }

    // Emitter awareness (2026-07-03): a "paddle"/"ball"-aimed emitter (Hell ball-spawners, Heaven
    // Seraph adds, etc.) fires a fresh hazard every EmitInterval for as long as it's alive — every
    // interval survived is a dodge a real player would rather not keep making. Emitter blocks are
    // always `needToKill` too, so finishing an already-damaged one off is pure upside: it stops the
    // barrage AND is required for the clear anyway. Lower priority than lava (which is literally
    // unavoidable once triggered) but this is what a "reasonably good" player facing a turret that's
    // already whittled down would naturally go finish, rather than let it keep shooting.
    private double? FindEmitterPriorityColX()
    {
        Block? best = null;
        double bestHpFrac = double.MaxValue;
        foreach (var b in _g.Blocks)
        {
            if (b.Dead || !b.Emitter || b.MaxHp <= 0) continue;
            if (b.EmitAim != "paddle" && b.EmitAim != "ball") continue;
            double frac = (double)b.Hp / b.MaxHp;
            if (frac < bestHpFrac) { bestHpFrac = frac; best = b; }
        }
        return best == null ? null : _g.Level.Grid.CellCenter(best.Col, best.Row).X;
    }

    // Boss-focus awareness (2026-07-03): a real player fighting a boss aims for the boss tiles
    // specifically — that's the obvious win condition. A random-angle ball instead spends most of
    // its hits on incidental fodder blocks around the boss, so damage-over-time kits (Fire Mage:
    // Ignite lights whatever the ball last touched, Conflagration only detonates blocks that are
    // ALREADY burning) rarely get their burst to land ON the boss at all — this showed up as Fire
    // Mage clearing 40-80% of a level's blocks while the boss itself sat near full HP, then dying to
    // ordinary attack attrition long before the boss was in reach. Engineer's Rocket already has
    // this exact behavior baked into the SPELL itself (see PickRocketTarget's boss-weak-point
    // priority) — this gives every other kit the same "obviously focus the boss" instinct instead of
    // penalizing whichever class's kit doesn't have it hardcoded.
    private double? FindBossPriorityColX()
    {
        var b = _g.Blocks.FirstOrDefault(x => !x.Dead && x.Boss);
        return b == null ? null : _g.Level.Grid.CellCenter(b.Col, b.Row).X;
    }

    // Portal awareness (2026-07-03): Village's ghost-phase levels gate roughly half their blocks
    // behind a phase the ball only gets by bouncing through a `village_portal` block — a deliberate
    // routing puzzle a real player aims for on purpose. A ball that never deliberately seeks the
    // portal plateaus at "whichever phase it happened to be in," reading as "level stuck at ~5-40%
    // cleared" even though nothing else is wrong. A real player interleaves phases rather than
    // waiting to fully exhaust one side first (that gate never actually fires — the bot dies before
    // ever fully clearing one phase) — so this fires probabilistically whenever the ball's CURRENT
    // phase can't reach some remaining NeedToKill block, same "sometimes, not obsessively" spirit as
    // the random catch angle everywhere else. A max-angle "hard commit" version was tried and
    // reverted — it measurably regressed village-10/11 for Paladin/Engineer (crowded out normal
    // ball-catching/dodging badly enough to drop them to literal 0% wins) even though it occasionally
    // reached the portal faster; the soft/probabilistic version below trades a lower per-attempt
    // success rate for not breaking survival elsewhere.
    private double? FindPortalPriorityColX()
    {
        var portal = _g.Blocks.FirstOrDefault(b => !b.Dead && b.Portal);
        if (portal == null) return null;
        bool ghostBlocksRemain  = _g.Blocks.Any(b => !b.Dead && b.NeedToKill && b.BallPhases);
        bool normalBlocksRemain = _g.Blocks.Any(b => !b.Dead && b.NeedToKill && !b.BallPhases);
        bool anyBallGhost  = _g.Balls.Any(b => b.Alive && b.Ghost);
        bool anyBallNormal = _g.Balls.Any(b => b.Alive && !b.Ghost);
        bool blockedFromGhost  = ghostBlocksRemain && !anyBallGhost;
        bool blockedFromNormal = normalBlocksRemain && !anyBallNormal;
        if (!blockedFromGhost && !blockedFromNormal) return null;
        if (_rng.NextDouble() > 0.6) return null; // don't abandon reachable blocks every single catch
        return _g.Level.Grid.CellCenter(portal.Col, portal.Row).X;
    }

    private double NextCatchAngle()
    {
        var priorityColX = FindLavaPriorityColX() ?? FindReviverPriorityColX() ?? FindPortalPriorityColX()
            ?? FindBossPriorityColX() ?? FindEmitterPriorityColX();
        if (!priorityColX.HasValue) return (_rng.NextDouble() * 2 - 1) * MaxRandomAngleFrac;
        double sign = Math.Sign(priorityColX.Value - _lastPaddleX);
        if (sign == 0) sign = _rng.NextDouble() < 0.5 ? -1 : 1;
        return sign * _rng.NextDouble() * MaxRandomAngleFrac;
    }

    // A burn-DoT-lit block is tagged with whichever spell lit it, using this burst-size signal:
    // an area-ignite effect (like Fire Wall) lights several blocks in the same tick; a single-target
    // ignite (like the Ignite imbue) lights exactly one. Only meaningful for fire-mage-style kits
    // with more than one ignite source in the loadout — harmless no-op for other classes.
    private const int AreaIgniteBurstThreshold = 3;

    // The sim's actual hit tests use <= (e.g. CombatSystem paddleBox.IntersectsCircle, BossSystem's
    // FistSlam Math.Abs(...) <= half+paddleHalf). An optimizer picking the position with fewest
    // overlaps will happily land EXACTLY on that boundary if nothing nudges it off — a real hit,
    // since the game's check is inclusive. Add a few pixels of cushion so "avoided" isn't a
    // pixel-exact coin flip against floating-point/timing jitter, and match <= not <.
    private const double SafetyMarginPx = 4.0;

    private readonly GameInstance _g;
    private readonly Random _rng;
    private double _nextCastCheck;
    private double _lastPaddleX;
    private double _currentCatchAngle;

    // ---- Results ----
    public readonly int Seed;
    public GamePhase FinalPhase;
    public double SimSeconds;
    public int Deflects, PerfectDeflects, BallsDropped, HpLost;
    public int BossPhaseTransitions;
    public int BossStartHp, BossEndHp;
    public int BlocksStart, BlocksEnd; // NeedToKill blocks on the LAST active floor (level-clear progress; 0 boss HP if no boss)
    public double BlocksClearedPct => BlocksStart > 0 ? 100.0 * (BlocksStart - BlocksEnd) / BlocksStart : 0;
    // Rift progress: 0/1 for a regular single-floor level. Set by the caller after construction.
    public int FloorsTotal = 1;
    public int FloorsCleared => _g.FloorIndex;

    // Level Score (2026-07-03): a single "how well did this run go" scalar. Requested to replace
    // per-spell damage as the balance signal — damage is only ever precisely measurable for a
    // couple of instant-effect spells (see SpellDamage's doc comment below), so a per-spell damage
    // table is mostly a wall of unmeasurable "—". Lower is always better; 0 is the unreachable
    // theoretical floor (instant clear, zero losses). A run that didn't WIN gets no partial credit
    // for how far it got — dying or timing out scores a flat FailScore, always worse than every
    // real clear no matter how slow or damaged.
    public const double FailScore = 1000.0;
    public const double SecondsPerLostHpOrBall = 15.0;
    public double LevelScore => FinalPhase == GamePhase.Won
        ? SimSeconds + (HpLost + BallsDropped) * SecondsPerLostHpOrBall
        : FailScore;
    public double MaxRequiredSpeed, SumRequiredSpeed;
    public int SpeedSamples, InfeasibleDodgeCount;
    // Damage attribution keyed by spell id (generic — works for any loadout, not just fire_mage).
    // Instant-effect spells (anything that deals damage synchronously at cast time, e.g.
    // Conflagration, Lightning, Reckoning) are measured exactly via before/after board HP around
    // every single cast. Burn-DoT spells (Ignite/Fire Wall) share one underlying tick mechanism,
    // so newly-burning blocks are tagged by the burst-size heuristic above and burn-tick damage is
    // split by tag. Whatever's left of a tick's HP loss is plain ball/board collision damage.
    public readonly Dictionary<string, int> SpellDamage = new();
    public int BallCollisionDamage;
    public readonly HashSet<int> IgnitedBlockIds = new();
    public readonly List<CastRecord> Casts = new();
    public readonly List<HitRecord> Hits = new();
    public readonly List<UndodgeableRecord> Undodgeable = new();
    public readonly List<SpeedSpikeRecord> SpeedSpikes = new();

    private readonly Dictionary<int, string> _blockIgniteSource = new();

    public record CastRecord(string Id, long Tick, double Time, double ManaBefore, double ManaCost, bool Consumed);
    public record HitRecord(long Tick, double Time, int HpAfter, string NearbyHazardKinds);
    public record UndodgeableRecord(long Tick, double Time, int HazardCount, string Kinds);
    public record SpeedSpikeRecord(long Tick, double Time, double Speed);

    public LevelBot(GameInstance g, int gameSeed, int rngSeed)
    {
        _g = g;
        Seed = gameSeed;
        _rng = new Random(rngSeed);
        _lastPaddleX = g.Paddle.Center.X;
        _currentCatchAngle = NextCatchAngle();
    }

    public void Run(double maxSimSeconds)
    {
        var dt = _g.Config.FixedDt;
        int maxTicks = (int)(maxSimSeconds / dt);
        BossStartHp = TotalBossHp();
        BlocksStart = AliveNeedToKillBlocks();
        int prevHp = _g.Hp;
        int prevSpares = _g.SpareBalls;
        var seenWaves = new HashSet<string>();

        for (int i = 0; i < maxTicks; i++)
        {
            if (_g.Phase == GamePhase.Serving) _g.Serve();
            if (_g.Phase is GamePhase.Won or GamePhase.Lost) break;
            if (_g.Phase != GamePhase.Playing) continue;

            // Continuous Rift: clearing a floor (but not the last) freezes the sim for a 1-of-3
            // modifier draft. A real player always has a pick available — take one at random so
            // the run doesn't stall.
            if (_g.AwaitingRiftDraft)
            {
                var choices = _g.RiftDraftChoices;
                if (choices.Count > 0) _g.PickRiftModifier(choices[_rng.Next(choices.Count)]);
                continue;
            }

            ScanForUndodgeableWaves(seenWaves);

            double target = ComputeTarget();
            double reqSpeed = Math.Abs(target - _lastPaddleX) / dt;
            MaxRequiredSpeed = Math.Max(MaxRequiredSpeed, reqSpeed);
            SumRequiredSpeed += reqSpeed;
            SpeedSamples++;
            if (reqSpeed > HumanSpeedThreshold)
            {
                InfeasibleDodgeCount++;
                if (SpeedSpikes.Count < 15) SpeedSpikes.Add(new SpeedSpikeRecord(_g.TickCount, _g.ElapsedPlayTime, reqSpeed));
            }
            // Execute at a capped speed, not a teleport — "reasonably good," not superhuman. When the
            // ideal target requires more than MaxPaddleSpeed, the bot falls genuinely short this tick.
            double maxStep = MaxPaddleSpeed * dt;
            double actualX = Math.Abs(target - _lastPaddleX) <= maxStep
                ? target
                : _lastPaddleX + Math.Sign(target - _lastPaddleX) * maxStep;
            _g.SetPaddleX(actualX);
            _lastPaddleX = actualX;

            // Damage attribution checkpoint 1: board HP right before the cast attempt.
            int hpBeforeCast = BoardHpTotal();
            string? castId = MaybeCastSpell();
            // Checkpoint 2: board HP right after the cast — an instant-damage spell (Conflagration,
            // Lightning, etc.) resolves synchronously inside CastSlot(), so any drop here is exactly
            // that spell's damage (imbue/aura spells deal zero HP at cast time — nothing to measure).
            int hpAfterCast = BoardHpTotal();
            int instantDmg = Math.Max(0, hpBeforeCast - hpAfterCast);
            if (instantDmg > 0 && castId != null)
                SpellDamage[castId] = SpellDamage.GetValueOrDefault(castId) + instantDmg;

            var burningBefore = new HashSet<int>();
            foreach (var b in _g.Blocks)
                if (!b.Dead && b.BurnRemaining > 0) { burningBefore.Add(b.Id); IgnitedBlockIds.Add(b.Id); }

            _g.Tick(dt);

            var newlyBurning = _g.Blocks.Where(b => !b.Dead && b.BurnRemaining > 0 && !burningBefore.Contains(b.Id)).ToList();
            string burnSourceTag = newlyBurning.Count >= AreaIgniteBurstThreshold ? "firewall" : "ignite";
            foreach (var b in newlyBurning)
            {
                IgnitedBlockIds.Add(b.Id);
                if (!_blockIgniteSource.ContainsKey(b.Id)) _blockIgniteSource[b.Id] = burnSourceTag;
            }

            int burnDmgThisTick = 0;
            bool lavaDrainThisTick = false;
            foreach (var e in _g.DrainEvents())
            {
                if (e.Kind == SimEventKind.Deflect)
                {
                    Deflects++;
                    _currentCatchAngle = NextCatchAngle();
                }
                else if (e.Kind == SimEventKind.PerfectDeflect) PerfectDeflects++;
                else if (e.Kind == SimEventKind.BossPhase) BossPhaseTransitions++;
                else if (e.Kind == SimEventKind.Burn) burnDmgThisTick += _g.Config.Fire.BurnDamage;
                else if (e.Kind == SimEventKind.FistTelegraph) _fistDangerColX = e.X;
                else if (e.Kind == SimEventKind.FistSlam) _fistDangerColX = null;
                else if (e.Kind == SimEventKind.LavaDrain) lavaDrainThisTick = true;
            }

            // Checkpoint 3: board HP after the tick — covers ball collisions AND the burn DoT tick.
            int hpAfterTick = BoardHpTotal();
            int tickDamage = Math.Max(0, hpAfterCast - hpAfterTick);
            int attributedBurn = Math.Min(burnDmgThisTick, tickDamage);
            if (attributedBurn > 0)
            {
                var burningTags = _g.Blocks.Where(b => !b.Dead && b.BurnRemaining > 0 && _blockIgniteSource.ContainsKey(b.Id))
                    .Select(b => _blockIgniteSource[b.Id]).ToList();
                int igniteBurning = burningTags.Count(t => t == "ignite");
                int firewallBurning = burningTags.Count(t => t == "firewall");
                int totalTagged = igniteBurning + firewallBurning;
                if (totalTagged > 0)
                {
                    int toFirewall = attributedBurn * firewallBurning / totalTagged;
                    if (toFirewall > 0) SpellDamage["firewall"] = SpellDamage.GetValueOrDefault("firewall") + toFirewall;
                    int toIgnite = attributedBurn - toFirewall;
                    if (toIgnite > 0) SpellDamage["ignite"] = SpellDamage.GetValueOrDefault("ignite") + toIgnite;
                }
                else SpellDamage["ignite"] = SpellDamage.GetValueOrDefault("ignite") + attributedBurn; // fallback
            }
            BallCollisionDamage += Math.Max(0, tickDamage - attributedBurn);

            if (_g.Hp < prevHp)
            {
                HpLost += prevHp - _g.Hp;
                var nearby = _g.Hazards
                    .Where(h => Math.Abs(h.Pos.X - target) < 60)
                    .Select(h => h.Kind)
                    .Distinct().ToList();
                // LavaDrain (LavaSystem.CheckDangerZone) calls DamagePlayer directly — no Hazards-list
                // entry exists at all, so it would otherwise show up as a misleading empty "[]" here
                // (the same blind-spot shape Fist Slam had before it got its own tracking).
                if (lavaDrainThisTick) nearby.Insert(0, "lavadrain");
                Hits.Add(new HitRecord(_g.TickCount, _g.ElapsedPlayTime, _g.Hp, string.Join(",", nearby)));
                prevHp = _g.Hp;
            }

            if (_g.SpareBalls < prevSpares)
            {
                BallsDropped += prevSpares - _g.SpareBalls;
                prevSpares = _g.SpareBalls;
            }
        }

        FinalPhase = _g.Phase;
        SimSeconds = _g.ElapsedPlayTime;
        BossEndHp = TotalBossHp();
        BlocksEnd = AliveNeedToKillBlocks();
    }

    private int TotalBossHp() => _g.Blocks.Where(b => b.Boss && !b.Dead).Sum(b => b.Hp);
    private int AliveNeedToKillBlocks() => _g.Blocks.Count(b => !b.Dead && b.NeedToKill);
    private int BoardHpTotal() => _g.Blocks.Sum(b => Math.Max(0, b.Hp));

    // ── Paddle control: predictive catch (random angle) + soonest-wave dodge ───

    private double ComputeTarget()
    {
        double boardW = _g.Level.Grid.Width;
        double half = _g.Paddle.Width / 2;
        double lo = half, hi = boardW - half;

        double desire = PredictCatchX(boardW) ?? _lastPaddleX;

        var blockers = SoonestHazardWave(out _);
        // Fist Slam (hell demon boss, AimedShot pattern — 60% likely in phase 1!) isn't a hazard-list
        // projectile at all: it locks onto whatever column the paddle occupies AT TELEGRAPH TIME and
        // deals direct HP damage 0.5s later if the paddle is still there. A hazard-only dodge model
        // never sees this and takes a free hit almost every AimedShot roll. Treat the locked column
        // (tracked via the FistTelegraph/FistSlam events — see event draining in Run) as an extra
        // "hazard" with the same avoid-it math, cleared once the slam resolves.
        if (_fistDangerColX.HasValue)
            blockers.Add((_fistDangerColX.Value, _g.Config.CellSize / 2, -1, "fistslam"));
        if (blockers.Count == 0) return Math.Clamp(desire, lo, hi);

        return FindBestPosition(blockers, lo, hi, desire, half);
    }

    private double? _fistDangerColX;

    /// <summary>All hazards whose arrival time at the paddle is within 0.08s of the soonest one
    /// (i.e. they threaten essentially simultaneously). Empty if no hazard is inbound.</summary>
    private List<(double x, double r, int id, string kind)> SoonestHazardWave(out double soonestT)
    {
        double paddleTopY = _g.Paddle.Center.Y - _g.Paddle.Height / 2;
        double boardW = _g.Level.Grid.Width;
        var all = new List<(double t, double x, double r, int id, string kind)>();
        foreach (var h in _g.Hazards)
        {
            if (!h.Alive || h.Vel.Y <= 0) continue;
            double t = (paddleTopY - h.Radius - h.Pos.Y) / h.Vel.Y;
            if (t < -0.05) continue;
            double x = h.Pos.X + h.Vel.X * t;
            if (x < -h.Radius * 2 || x > boardW + h.Radius * 2) continue; // exits the board before arriving
            all.Add((t, x, h.Radius, h.Id, h.Kind));
        }
        double soonest = all.Count > 0 ? all.Min(a => a.t) : double.MaxValue;
        soonestT = soonest;
        return all.Where(a => a.t - soonest < 0.08)
                  .Select(a => (a.x, a.r, a.id, a.kind))
                  .ToList();
    }

    /// <summary>Sample paddle-center positions across the valid range; pick the one overlapping the
    /// fewest incoming hazards (0 if a fully-safe spot exists), tie-broken by distance to `desire`.</summary>
    private static double FindBestPosition(List<(double x, double r, int id, string kind)> blockers,
        double lo, double hi, double desire, double half)
    {
        const double step = 2.0;
        double bestPos = Math.Clamp(desire, lo, hi);
        int bestOverlap = int.MaxValue;
        double bestDist = double.MaxValue;
        for (double p = lo; p <= hi; p += step)
        {
            int overlap = 0;
            foreach (var b in blockers)
                if (Math.Abs(p - b.x) <= b.r + half + SafetyMarginPx) overlap++;
            double dist = Math.Abs(p - Math.Clamp(desire, lo, hi));
            if (overlap < bestOverlap || (overlap == bestOverlap && dist < bestDist))
            {
                bestOverlap = overlap;
                bestDist = dist;
                bestPos = p;
            }
        }
        return bestPos;
    }

    /// <summary>Predicts where a descending ball will cross the paddle's Y line, folding the
    /// trajectory off the side walls (balls bounce; hazards don't), then offsets the catch by a
    /// randomized angle (regenerated each deflect — see Deflect handling in Run) rather than a
    /// deliberately steered one. Falls back to the nearest alive ball's raw X if none is descending.</summary>
    private double? PredictCatchX(double boardW)
    {
        double paddleY = _g.Paddle.Center.Y - _g.Paddle.Height / 2;
        Ball? target = null;
        double bestT = double.MaxValue;
        foreach (var b in _g.Balls)
        {
            if (!b.Alive || b.Vel.Y <= 0) continue;
            double t = (paddleY - b.Radius - b.Pos.Y) / b.Vel.Y;
            if (t < 0 || t >= bestT) continue;
            bestT = t;
            target = b;
        }
        if (target == null)
            return _g.Balls.Where(b => b.Alive).OrderByDescending(b => b.Pos.Y).FirstOrDefault()?.Pos.X;

        double lo = target.Radius, hi = boardW - target.Radius;
        double range = hi - lo;
        double ballArrivalX;
        if (range <= 0) ballArrivalX = target.Pos.X;
        else
        {
            double raw = target.Pos.X + target.Vel.X * bestT;
            double rel = (raw - lo) % (2 * range);
            if (rel < 0) rel += 2 * range;
            ballArrivalX = rel <= range ? lo + rel : lo + (2 * range - rel);
        }

        double half = _g.Paddle.Width / 2;
        return ballArrivalX - _currentCatchAngle * half;
    }

    // ── Undodgeable-moment detection (independent of what the bot actually does) ─

    private void ScanForUndodgeableWaves(HashSet<string> seenWaves)
    {
        var wave = SoonestHazardWave(out _);
        if (wave.Count < 2) return; // a lone hazard always has room on a board wider than paddle+hazard

        string waveKey = string.Join(",", wave.Select(w => w.id).OrderBy(id => id));
        if (!seenWaves.Add(waveKey)) return; // already reported this exact wave

        double boardW = _g.Level.Grid.Width;
        double half = _g.Paddle.Width / 2;
        double lo = half, hi = boardW - half;

        bool anySafe = false;
        for (double p = lo; p <= hi; p += 2.0)
        {
            bool blocked = false;
            foreach (var w in wave)
                // No safety margin here (unlike FindBestPosition): this scan asks "does ANY position
                // exist at all," so it should use the game's exact hit boundary, not a cushioned one —
                // padding it would manufacture false "undodgeable" findings.
                if (Math.Abs(p - w.x) <= w.r + half) { blocked = true; break; }
            if (!blocked) { anySafe = true; break; }
        }

        if (!anySafe)
        {
            Undodgeable.Add(new UndodgeableRecord(_g.TickCount, _g.ElapsedPlayTime, wave.Count,
                string.Join("+", wave.Select(w => w.kind).Distinct())));
        }
    }

    // ── Spell casting: random pick among affordable, not-visibly-disabled loadout slots ─

    /// <summary>Mirrors the live HUD's disabled-button gate (Hud.svelte `needsFire`) — a real
    /// player never taps a visibly locked spell, so the bot shouldn't "cast" one either just to
    /// rack up fizzle attempts. Only Conflagration has such a gate today; extend here if others
    /// grow one (see the HUD needsFire check for the source of truth on the condition).</summary>
    private bool IsHudLocked(string spellId)
        => spellId == "fireball" && !_g.Blocks.Any(b => !b.Dead && b.BurnRemaining > 0);

    private string? MaybeCastSpell()
    {
        // Reactive defensive cast (2026-07-03): a barrier-type spell (Shield/Fire Wall) is exactly
        // the tool a "reasonably good" player reaches for when something is actively inbound — not
        // on a random timer regardless of what's on screen. Without this, the random-cast policy
        // wastes Shield's short lifetime (4s base) on idle moments half the time, then has zero
        // uptime exactly when a barrage rolls through. This is what surfaced Paladin — whose only
        // anti-projectile tool IS Shield — losing to hell-4/hell-11's ballspawner emitters far more
        // than classes with no such tool at all: not the spell being weak, the bot never timing it.
        // Bypasses the normal cast-cooldown throttle — a real player doesn't wait out an idle timer
        // to react to an incoming volley.
        if (_g.Barriers.Count == 0 && (_g.Hazards.Any(h => h.Alive) || _fistDangerColX.HasValue))
        {
            for (int i = 0; i < _g.Loadout.Count; i++)
            {
                var bdef = _g.GetSpellDef(_g.Loadout[i]);
                if (bdef == null || bdef.Archetype != SpellArchetype.Placement) continue;
                if (bdef.PlacementKind != "barrier" && bdef.PlacementKind != "firewall") continue;
                if (_g.ManaValue < bdef.ManaCost) continue;
                return CastSlot(i);
            }
        }

        // Vertical-strike reactive cast (2026-07-03): a straight-shot Projectile spell with no
        // homing (Paladin's Spear: launches from wherever the paddle IS, straight up, no steering
        // afterward) only ever hits the boss if the paddle happens to be lined up with it AT CAST
        // TIME. Casting on the normal random timer usually fires down an unrelated column — a real
        // player fires when they notice they're actually lined up. Analogous to Conflagration's boss
        // splash (ConflagrationSystem.Cast) but for a spell type that has no "splash" to fall back on.
        var bossForStrike = _g.Blocks.FirstOrDefault(b => !b.Dead && b.Boss);
        if (bossForStrike != null)
        {
            double bossColX = _g.Level.Grid.CellCenter(bossForStrike.Col, bossForStrike.Row).X;
            if (Math.Abs(_lastPaddleX - bossColX) < _g.Paddle.Width / 2)
            {
                for (int i = 0; i < _g.Loadout.Count; i++)
                {
                    var sdef = _g.GetSpellDef(_g.Loadout[i]);
                    if (sdef == null || sdef.Archetype != SpellArchetype.Projectile || sdef.Homing) continue;
                    if (_g.ManaValue < sdef.ManaCost) continue;
                    return CastSlot(i);
                }
            }
        }

        if (_g.ElapsedPlayTime < _nextCastCheck) return null;

        var candidates = new List<int>();
        for (int i = 0; i < _g.Loadout.Count; i++)
        {
            var def = _g.GetSpellDef(_g.Loadout[i]);
            if (def != null && _g.ManaValue >= def.ManaCost && !IsHudLocked(_g.Loadout[i])) candidates.Add(i);
        }
        if (candidates.Count == 0) { _nextCastCheck = _g.ElapsedPlayTime + 0.2; return null; }

        // Uniform-random among affordable candidates would systematically starve expensive spells:
        // a free/cheap spell (Ignite, Decay) is affordable almost every check, so it wins the random
        // draw far more often than a 70-80 mana spell that's only briefly affordable right after
        // enough regen accumulates — before something cheap grabs the mana first. Bias a fraction of
        // decisions toward the priciest affordable option ("save up for the big one" sometimes) so
        // every spell in a large test loadout gets genuine exercise, not just the free ones.
        int slot = _rng.NextDouble() < 0.35
            ? candidates.OrderByDescending(i => _g.GetSpellDef(_g.Loadout[i])!.ManaCost).First()
            : candidates[_rng.Next(candidates.Count)];
        string? result = CastSlot(slot);
        _nextCastCheck = _g.ElapsedPlayTime + 0.3 + _rng.NextDouble() * 0.7;
        return result;
    }

    private string? CastSlot(int slot)
    {
        string id = _g.Loadout[slot];
        double manaBefore = _g.ManaValue;
        double cost = _g.GetSpellDef(id)!.ManaCost;

        _g.CastSlot(slot);

        double manaAfter = _g.ManaValue;
        // A zero-cost spell (e.g. a free signature imbue) can't be detected via mana delta —
        // Spend() always succeeds for cost 0 as long as we're in Playing (already guaranteed by
        // the candidate filter above).
        bool consumed = cost > 1e-9 ? (manaBefore - manaAfter > 1e-6) : true;
        Casts.Add(new CastRecord(id, _g.TickCount, _g.ElapsedPlayTime, manaBefore, cost, consumed));
        return consumed ? id : null;
    }

    // ── Reporting ────────────────────────────────────────────────────────────

    private static string Pct(int part, int total) => total > 0 ? $"{100.0 * part / total:F0}%" : "n/a";

    public void PrintReport(ITestOutputHelper output)
    {
        output.WriteLine("==================== LEVEL BALANCE BOT REPORT ====================");
        output.WriteLine($"Outcome:                  {FinalPhase}");
        output.WriteLine($"Sim time:                 {SimSeconds:F1}s ({(long)(SimSeconds * 60)} ticks)");
        output.WriteLine($"Blocks cleared:           {BlocksStart - BlocksEnd}/{BlocksStart} ({BlocksClearedPct:F0}%)");
        if (BossStartHp > 0) output.WriteLine($"Boss HP:                  {BossStartHp} -> {BossEndHp}");
        output.WriteLine($"Boss phase transitions:   {BossPhaseTransitions}");
        output.WriteLine($"Balls reflected (catches):{Deflects}  (perfect: {PerfectDeflects})");
        output.WriteLine($"Balls dropped (missed):   {BallsDropped}");
        output.WriteLine($"Player HP lost:           {HpLost}  (final HP {_g.Hp}/{_g.Config.StartHp})");
        output.WriteLine($"Level Score:              {LevelScore:F0}  (lower is better; {FailScore:F0} = loss/timeout, no partial credit)");
        output.WriteLine($"Unique blocks ignited:    {IgnitedBlockIds.Count}");
        int totalDamage = SpellDamage.Values.Sum() + BallCollisionDamage;
        output.WriteLine($"Damage breakdown (of {totalDamage} total board damage dealt):");
        foreach (var (id, dmg) in SpellDamage.OrderByDescending(kv => kv.Value))
            output.WriteLine($"    {id,-24}{dmg,4}  ({Pct(dmg, totalDamage)})");
        output.WriteLine($"    {"ball/board collisions",-24}{BallCollisionDamage,4}  ({Pct(BallCollisionDamage, totalDamage)})");
        double avgSpeed = SpeedSamples > 0 ? SumRequiredSpeed / SpeedSamples : 0;
        output.WriteLine($"Required instant paddle speed: max={MaxRequiredSpeed:F0}px/s avg={avgSpeed:F0}px/s " +
                          "(sim allows teleport; a real player's max flick speed is roughly 1000-2000px/s)");
        output.WriteLine("");

        output.WriteLine($"--- Spell casts ({Casts.Count} attempts) ---");
        foreach (var group in Casts.GroupBy(c => c.Id))
        {
            int total = group.Count();
            int useful = group.Count(c => c.Consumed);
            output.WriteLine($"  {group.Key,-10} attempted={total,3}  landed={useful,3}  fizzled={total - useful,3}");
        }
        output.WriteLine("");

        output.WriteLine($"--- Hits taken ({Hits.Count}) ---");
        foreach (var h in Hits)
            output.WriteLine($"    t={h.Time,6:F1}s  HP now={h.HpAfter}  nearby hazard kind(s)={h.NearbyHazardKinds}");
        output.WriteLine("");

        output.WriteLine($"--- Undodgeable moments detected ({Undodgeable.Count}) ---");
        output.WriteLine("    (a wave where literally NO paddle position on the whole board avoids every hazard)");
        if (Undodgeable.Count == 0)
            output.WriteLine("    NONE — every wave of simultaneous hazards had at least one safe paddle position.");
        foreach (var u in Undodgeable)
            output.WriteLine($"    t={u.Time,6:F1}s  {u.HazardCount} simultaneous hazards ({u.Kinds}) covered the ENTIRE board width — no paddle position could avoid all of them.");
        output.WriteLine("");

        output.WriteLine($"--- Humanly-infeasible dodges ({InfeasibleDodgeCount} ticks over {HumanSpeedThreshold:F0}px/s) ---");
        output.WriteLine("    (a safe spot existed, but reaching it required an instantaneous jump faster than any real flick/drag)");
        if (SpeedSpikes.Count == 0)
            output.WriteLine("    NONE — every dodge was reachable at a humanly plausible speed.");
        foreach (var s in SpeedSpikes)
            output.WriteLine($"    t={s.Time,6:F1}s  required {s.Speed,7:F0}px/s");
        if (InfeasibleDodgeCount > SpeedSpikes.Count) output.WriteLine($"    ... and {InfeasibleDodgeCount - SpeedSpikes.Count} more ticks over threshold");
        output.WriteLine("===========================================================");
    }
}
