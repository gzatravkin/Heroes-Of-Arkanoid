/**
 * Google Play Games Services — REST API wrapper.
 *
 * The access token comes from Firebase Google Sign-In with the GPGS scope
 * (see FirebaseAuth.ts → linkGoogle). Leaderboard and achievement IDs are
 * created in the Google Play Console and pasted into .env as VITE_GPGS_*.
 *
 * These calls are fire-and-forget; failures never block gameplay.
 */

const BASE = "https://www.googleapis.com/games/v1";

// ── Resource IDs from .env (set after creating them in Play Console) ──────────

export const PLAY_LB = {
  souls_weekly: import.meta.env.VITE_GPGS_LB_SOULS_WEEKLY ?? "",
  progression:  import.meta.env.VITE_GPGS_LB_PROGRESSION  ?? "",
} as const;

export const PLAY_ACH = {
  first_win:         import.meta.env.VITE_GPGS_ACH_FIRST_WIN         ?? "",
  campaign_complete: import.meta.env.VITE_GPGS_ACH_CAMPAIGN_COMPLETE ?? "",
  beat_boss:         import.meta.env.VITE_GPGS_ACH_BEAT_BOSS         ?? "",
  perfect_run:       import.meta.env.VITE_GPGS_ACH_PERFECT_RUN       ?? "",
  diamond_league:    import.meta.env.VITE_GPGS_ACH_DIAMOND_LEAGUE    ?? "",
} as const;

// ── Helpers ───────────────────────────────────────────────────────────────────

async function gPost(path: string, token: string): Promise<boolean> {
  try {
    const r = await fetch(`${BASE}${path}`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}` },
    });
    return r.ok;
  } catch { return false; }
}

// ── Public API ────────────────────────────────────────────────────────────────

/** Submit an integer score to a Play Games leaderboard. */
export async function gpgsSubmitScore(lbId: string, score: number, token: string): Promise<void> {
  if (!lbId || !token || score < 0) return;
  await gPost(`/leaderboards/${encodeURIComponent(lbId)}/scores?score=${score}`, token);
}

/** Unlock a Play Games achievement. */
export async function gpgsUnlockAchievement(achId: string, token: string): Promise<void> {
  if (!achId || !token) return;
  await gPost(`/achievements/${encodeURIComponent(achId)}/unlock`, token);
}

/** Get the signed-in player's Play Games profile. */
export async function gpgsGetPlayer(token: string): Promise<{ id: string; displayName: string; avatarUrl: string } | null> {
  if (!token) return null;
  try {
    const r = await fetch(`${BASE}/players/me`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!r.ok) return null;
    const d = await r.json();
    return { id: d.playerId ?? "", displayName: d.displayName ?? "", avatarUrl: d.avatarImageUrl ?? "" };
  } catch { return null; }
}
