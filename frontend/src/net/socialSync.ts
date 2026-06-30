/**
 * socialSync — single call site for all post-win social writes.
 * Imported by campaignFlow.ts; all calls are fire-and-forget (errors never block the overlay).
 */
import { submitSouls, syncProgression, syncProfile } from "./FirestoreSync";
import { recordEventClear } from "./FirestoreEvents";
import { gpgsSubmitScore, gpgsUnlockAchievement, PLAY_LB, PLAY_ACH } from "./GooglePlayGames";
import { currentAccessToken } from "./FirebaseAuth";

export async function onLevelWin(opts: {
  level:      string;
  levelIndex: number;
  soulsEarned: number;
  profile:    any;
  isBoss?:    boolean;
  isPerfect?: boolean;
}): Promise<void> {
  const { level, levelIndex, soulsEarned, profile, isBoss, isPerfect } = opts;
  const token = currentAccessToken();

  const tasks: Promise<unknown>[] = [
    submitSouls(soulsEarned),
    syncProgression(profile, level, levelIndex),
    syncProfile(profile),
    recordEventClear(),
  ];

  // Google Play Games leaderboards
  if (token) {
    if (PLAY_LB.souls_weekly)
      tasks.push(gpgsSubmitScore(PLAY_LB.souls_weekly, soulsEarned, token));
    if (PLAY_LB.progression)
      tasks.push(gpgsSubmitScore(PLAY_LB.progression, levelIndex, token));
  }

  // Google Play achievements
  const achQueue: string[] = [];
  if (isBoss)    achQueue.push(PLAY_ACH.beat_boss);
  if (isPerfect) achQueue.push(PLAY_ACH.perfect_run);
  if (level === "heaven-boss") achQueue.push(PLAY_ACH.campaign_complete);
  // first_win fires on every win (GPGS deduplicates internally)
  achQueue.push(PLAY_ACH.first_win);

  if (token) {
    for (const id of achQueue) {
      if (id) tasks.push(gpgsUnlockAchievement(id, token));
    }
  }

  await Promise.allSettled(tasks);
}
