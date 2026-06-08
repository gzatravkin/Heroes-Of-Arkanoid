using System.Linq;
using Arkanoid.Core.Entities;
using Arkanoid.Core.Grid;
using Arkanoid.Core.Math;
namespace Arkanoid.Core.Sim;

public sealed class GameInstance
{
    public SimConfig Config { get; }
    public LevelData Level { get; }
    public Rng Rng { get; private set; }

    public GamePhase Phase { get; private set; } = GamePhase.Serving;
    public int Lives { get; private set; }
    public int SpareBalls { get; private set; }

    public Paddle Paddle { get; }
    public List<Ball> Balls { get; } = new();
    public List<Block> Blocks => Level.Blocks;

    private int _nextBallId = 1;
    private int _nextProjId = 1;
    public List<Projectile> Projectiles { get; } = new();
    private readonly ISimLog _log;
    public long TickCount { get; private set; }

    public GameInstance(LevelData level, SimConfig config, int seed, ISimLog? log = null)
    {
        Level = level; Config = config; Rng = new Rng(seed); _log = log ?? NullSimLog.Instance;
        Lives = config.StartLives;
        SpareBalls = config.StartBalls;
        Paddle = new Paddle {
            Width = config.PaddleWidth,
            Height = config.PaddleHeight,
            Center = new Vec2(level.Grid.Width / 2.0, level.Grid.Height + config.CellSize)
        };
        SpawnBallOnPaddle();
        _log.Log(0, "init", "instance created", $"level={level.Id} seed={seed} blocks={Blocks.Count} lives={Lives} balls={SpareBalls}");
    }

    private void SpawnBallOnPaddle()
    {
        Balls.Clear();
        Balls.Add(new Ball {
            Id = _nextBallId++,
            Radius = Config.BallRadius,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height / 2 - Config.BallRadius - 1),
            Vel = new Vec2(0, 0),
            Alive = true
        });
        _igniteArmed = false;   // discard any unused arm so it doesn't leak to the next life
        Phase = GamePhase.Serving;
    }

    public void Serve()
    {
        if (Phase != GamePhase.Serving) return;
        // launch upward with a small deterministic horizontal lean
        var lean = Rng.Range(-0.25, 0.25);
        Balls[0].Vel = new Vec2(lean, -1).Normalized() * Config.BallSpeed;
        Phase = GamePhase.Playing;
        _log.Log(TickCount, "serve", "ball launched", $"lean={lean:F3} vx={Balls[0].Vel.X:F1} vy={Balls[0].Vel.Y:F1}");
    }

    public void SetPaddleX(double x)
    {
        var half = Paddle.Width / 2;
        var clamped = System.Math.Clamp(x, half, Level.Grid.Width - half);
        Paddle.Center = new Vec2(clamped, Paddle.Center.Y);
    }

    public void Tick(double dt)
    {
        if (Phase != GamePhase.Playing) return;
        TickCount++;
        if (_log.Verbose)
            _log.Log(TickCount, "tick", "", $"balls={Balls.Count(b=>b.Alive)} mana={ManaValue:F0} blocks={Blocks.Count(b=>!b.Dead)}");
        RegenMana(dt);
        foreach (var b in Balls)
        {
            if (!b.Alive) continue;
            b.Pos += b.Vel * dt;
            if (_log.Verbose) _log.Log(TickCount, "ball", "move", $"id={b.Id} x={b.Pos.X:F1} y={b.Pos.Y:F1}");
            Arkanoid.Core.Physics.BallPhysics.ResolveWalls(b, Level.Grid.Width, Config);
            if (Arkanoid.Core.Physics.BallPhysics.ResolvePaddle(b, Paddle, Config, out var t))
                OnPaddleHit(b, t);
            ResolveBlocks(b);
        }
        UpdateProjectiles(dt);
        ResolveDrainAndWin();
    }

    private void RegenMana(double dt)
        => ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaRegenPerSec * dt);

    private void OnPaddleHit(Ball b, double t)
    {
        _log.Log(TickCount, "paddle", "deflect", $"t={t:F2} vx={b.Vel.X:F1} vy={b.Vel.Y:F1}");
        if (System.Math.Abs(t) < Config.PerfectDeflectBand)
        {
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerfectDeflectBonus);
            _log.Log(TickCount, "mana", "perfect deflect bonus", $"mana={ManaValue:F0}");
        }
        ApplyIgniteOnDeflect(b);
    }

    private void ApplyIgniteOnDeflect(Ball b)
    {
        if (!_igniteArmed) return;
        b.IgniteHitsLeft = Config.IgniteHits;
        _igniteArmed = false;
        _log.Log(TickCount, "ignite", "imbued ball", $"id={b.Id} hits={b.IgniteHitsLeft}");
        RaiseEvent("ignite", b.Pos.X, b.Pos.Y);
    }
    private void ResolveBlocks(Ball b)
    {
        var cell = Config.CellSize;
        // NOTE: on simultaneous overlap of two blocks (corner), we resolve the FIRST in
        // list order (deterministic). Sign-based reflection prevents sticking; exact face
        // selection is a known feel item deferred to the M1 demo pass.
        foreach (var blk in Blocks)
        {
            if (blk.Dead) continue;
            var c = Level.Grid.CellCenter(blk.Col, blk.Row);
            var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
            if (!box.IntersectsCircle(b.Pos, b.Radius)) continue;

            // reflect by dominant penetration axis
            var dx = b.Pos.X - c.X; var dy = b.Pos.Y - c.Y;
            if (System.Math.Abs(dx) / (cell / 2) > System.Math.Abs(dy) / (cell / 2))
                b.Vel = new Arkanoid.Core.Math.Vec2(System.Math.Sign(dx) * System.Math.Abs(b.Vel.X), b.Vel.Y);
            else
                b.Vel = new Arkanoid.Core.Math.Vec2(b.Vel.X, System.Math.Sign(dy) * System.Math.Abs(b.Vel.Y));

            var bonus = b.IgniteHitsLeft > 0 ? 1 : 0;
            DamageBlock(blk, Config.BallDamage + bonus, igniteSource: b.IgniteHitsLeft > 0);
            if (b.IgniteHitsLeft > 0) b.IgniteHitsLeft--;
            break; // one block per tick keeps it deterministic
        }
    }

    private void DamageBlock(Block blk, int dmg, bool igniteSource)
    {
        blk.Hp -= dmg;
        _log.Log(TickCount, "block", blk.Hp <= 0 ? "destroyed" : "hit",
                 $"id={blk.Id} col={blk.Col} row={blk.Row} hp={blk.Hp} dmg={dmg} ignite={igniteSource}");
        if (blk.Hp <= 0 && !blk.Dead)
        {
            blk.Dead = true;
            var c = Level.Grid.CellCenter(blk.Col, blk.Row);
            RaiseEvent("blockDestroyed", c.X, c.Y);
            ManaValue = System.Math.Min(Config.ManaMax, ManaValue + Config.ManaPerKill);
            if (igniteSource) SpreadFire(blk);
        }
    }

    private void SpreadFire(Block origin)
    {
        (int dc, int dr)[] n = { (1,0), (-1,0), (0,1), (0,-1) };
        foreach (var (dc, dr) in n)
        {
            var nb = Blocks.FirstOrDefault(b => !b.Dead && b.Col == origin.Col + dc && b.Row == origin.Row + dr);
            if (nb != null)
            {
                nb.Hp -= 1; // chip
                _log.Log(TickCount, "burn", "spread chip", $"id={nb.Id} hp={nb.Hp}");
                var c = Level.Grid.CellCenter(nb.Col, nb.Row);
                RaiseEvent("burn", c.X, c.Y);
                if (nb.Hp <= 0) { nb.Dead = true; RaiseEvent("blockDestroyed", c.X, c.Y); }
            }
        }
    }
    private void ResolveDrainAndWin()
    {
        if (!Blocks.Any(b => b.NeedToKill && !b.Dead))
        {
            Phase = GamePhase.Won;
            _log.Log(TickCount, "win", "all needToKill cleared");
            RaiseEvent("levelWon", 0, 0);
            return;
        }
        var drainLine = Level.Grid.Height + Config.CellSize * 2;
        foreach (var b in Balls)
            if (b.Alive && b.Pos.Y - b.Radius > drainLine)
            { b.Alive = false; _log.Log(TickCount, "drain", "ball lost", $"id={b.Id}"); }

        if (Balls.All(b => !b.Alive))
        {
            if (SpareBalls <= 0)
            { Phase = GamePhase.Lost; _log.Log(TickCount, "lose", "out of spare balls"); RaiseEvent("levelLost", 0, 0); return; }
            SpareBalls--;
            _log.Log(TickCount, "reserve", "re-serve", $"spareBalls={SpareBalls}");
            SpawnBallOnPaddle();
        }
    }

    // --- resources/events surface ---
    public double ManaValue { get; set; } = 0;
    private readonly List<Arkanoid.Core.Net.EventDto> _events = new();
    public void RaiseEvent(string type, double x, double y)
        => _events.Add(new Arkanoid.Core.Net.EventDto { Type = type, X = x, Y = y });
    public List<Arkanoid.Core.Net.EventDto> DrainEvents()
    { var copy = new List<Arkanoid.Core.Net.EventDto>(_events); _events.Clear(); return copy; }

    public void CastFireball()
    {
        if (Phase != GamePhase.Playing) return;
        if (ManaValue < Config.FireballCost)
        { _log.Log(TickCount, "spell", "fireball denied", $"mana={ManaValue:F0} need={Config.FireballCost}"); return; }
        ManaValue -= Config.FireballCost;
        Projectiles.Add(new Projectile {
            Id = _nextProjId++,
            Pos = new Vec2(Paddle.Center.X, Paddle.Center.Y - Paddle.Height),
            Vel = new Vec2(0, -Config.FireballSpeed),
            Damage = Config.FireballDamage, Radius = Config.BallRadius
        });
        _log.Log(TickCount, "spell", "fireball cast", $"mana={ManaValue:F0}");
        RaiseEvent("spellCast", Paddle.Center.X, Paddle.Center.Y);
    }

    private void UpdateProjectiles(double dt)
    {
        var cell = Config.CellSize;
        foreach (var pr in Projectiles)
        {
            if (!pr.Alive) continue;
            pr.Pos += pr.Vel * dt;
            if (pr.Pos.Y < -cell) { pr.Alive = false; continue; }
            foreach (var blk in Blocks)
            {
                if (blk.Dead) continue;
                var c = Level.Grid.CellCenter(blk.Col, blk.Row);
                var box = Arkanoid.Core.Math.Aabb.FromCenter(c, cell / 2, cell / 2);
                if (box.IntersectsCircle(pr.Pos, pr.Radius))
                { DamageBlock(blk, pr.Damage, igniteSource: false); pr.Alive = false; break; }
            }
        }
        Projectiles.RemoveAll(p => !p.Alive);
    }

    private bool _igniteArmed = false;

    public void CastIgnite()
    {
        if (Phase != GamePhase.Playing) return;
        if (ManaValue < Config.IgniteCost) return;
        ManaValue -= Config.IgniteCost; // cost is 0 by default (anti-Wizorb)
        _igniteArmed = true;
        _log.Log(TickCount, "ignite", "armed", $"mana={ManaValue:F0}");
        RaiseEvent("spellCast", Paddle.Center.X, Paddle.Center.Y);
    }
    public void ApplyCheat(string op, double value)
    {
        _log.Log(TickCount, "cheat", op, $"value={value}");
        switch (op)
        {
            case "clearAllButN":
                var keep = (int)value;
                var alive = Blocks.Where(b => !b.Dead).ToList();
                for (int i = 0; i < alive.Count - keep; i++) alive[i].Dead = true;
                break;
            case "winNow":
                foreach (var b in Blocks) b.Dead = true;
                Phase = GamePhase.Won; RaiseEvent("levelWon", 0, 0);
                break;
            case "loseNow":
                Phase = GamePhase.Lost; RaiseEvent("levelLost", 0, 0);
                break;
            case "setSeed": Rng = new Rng((int)value); break;
            case "setMana": ManaValue = System.Math.Clamp(value, 0, Config.ManaMax); break;
            case "loseBall":
                foreach (var b in Balls) b.Alive = false;
                break;
        }
    }
}
