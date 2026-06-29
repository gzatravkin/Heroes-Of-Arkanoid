using Arkanoid.Core.Sim;
namespace Arkanoid.Core.Meta;

/// <summary>Rotating weekly event logic (plan §C): a board-changing run modifier, per-event tokens, and a
/// one-shot milestone reward. Resets when the event rotates. Pure.</summary>
public static class EventService
{
    public const string BoardPrefix = "event-";

    public static void EnsureEvent(Profile p, int weekId)
    {
        if (p.Season.EventWeek == weekId) return;
        p.Season.EventWeek = weekId;
        p.Season.EventTokens = 0;
        p.Season.EventClaimed = false;
    }

    public static void AddTokens(Profile p, int weekId, int amount)
    {
        if (amount <= 0) return;
        EnsureEvent(p, weekId);
        p.Season.EventTokens += amount;
    }

    /// <summary>Claim the event's milestone reward once enough event tokens are earned. Idempotent.</summary>
    public static SeasonClaimResult ClaimMilestone(Profile p, EventDef ev, int weekId)
    {
        EnsureEvent(p, weekId);
        if (p.Season.EventClaimed || p.Season.EventTokens < ev.MilestoneTokens)
            return new SeasonClaimResult { Ok = false };
        p.Season.EventClaimed = true;
        Wallet.Add(p, Currency.Sparks, ev.RewardModuleCores);
        Wallet.Add(p, Currency.Souls,    ev.RewardGems);
        return new SeasonClaimResult { Ok = true, ModuleCores = ev.RewardModuleCores, Gems = ev.RewardGems };
    }

    /// <summary>Apply the live event's run modifier to a battle (this is what "changes the board").</summary>
    public static void ApplyModifier(GameInstance game, EventDef ev)
        => RunModifier.Apply(ev.Effect, ev.Magnitude, "", game);
}
