import { doc, setDoc, getDoc, increment, serverTimestamp } from "firebase/firestore";
import { fbDb, isFirebaseConfigured } from "./firebase";
import { currentUid, currentNickname } from "./FirebaseAuth";

// ── Period helpers (UTC-aligned, deterministic) ───────────────────────────────

function dayStartMs(d = new Date()): number {
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
}

function weekStartMs(d = new Date()): number {
  const utc = dayStartMs(d);
  const dow  = new Date(utc).getUTCDay(); // 0 = Sunday
  return utc - ((dow + 6) % 7) * 86_400_000; // roll back to Monday
}

function monthStartMs(d = new Date()): number {
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1);
}

// ── Profile backup ────────────────────────────────────────────────────────────

export async function syncProfile(profile: unknown): Promise<void> {
  if (!isFirebaseConfigured()) return;
  const uid = currentUid();
  if (!uid) return;
  try {
    await setDoc(
      doc(fbDb(), "users", uid),
      { profileSnapshot: JSON.stringify(profile), profileUpdatedAt: serverTimestamp() },
      { merge: true },
    );
  } catch { /* best-effort */ }
}

export async function fetchCloudProfile(): Promise<unknown | null> {
  if (!isFirebaseConfigured()) return null;
  const uid = currentUid();
  if (!uid) return null;
  try {
    const snap = await getDoc(doc(fbDb(), "users", uid));
    if (!snap.exists()) return null;
    const raw = snap.data().profileSnapshot;
    return raw ? JSON.parse(raw) : null;
  } catch { return null; }
}

// ── Souls leaderboard (time-windowed, client-side period detection) ───────────

export async function submitSouls(amount: number): Promise<void> {
  if (!isFirebaseConfigured() || amount <= 0) return;
  const uid      = currentUid();
  const nickname = currentNickname();
  if (!uid || !nickname) return;

  const now     = new Date();
  const periods = [
    { col: "souls_day",   start: dayStartMs(now)   },
    { col: "souls_week",  start: weekStartMs(now)  },
    { col: "souls_month", start: monthStartMs(now) },
  ] as const;

  await Promise.allSettled(periods.map(async ({ col, start }) => {
    const ref  = doc(fbDb(), "lb", col, "scores", uid);
    const snap = await getDoc(ref);
    if (snap.exists() && snap.data().periodStart === start) {
      await setDoc(ref, { souls: increment(amount), updatedAt: serverTimestamp() }, { merge: true });
    } else {
      await setDoc(ref, { nickname, souls: amount, periodStart: start, updatedAt: serverTimestamp() });
    }
  }));
}

// ── Progression leaderboard ───────────────────────────────────────────────────

export async function syncProgression(
  profile: any,
  levelId: string,
  levelIndex: number,
): Promise<void> {
  if (!isFirebaseConfigured()) return;
  const uid      = currentUid();
  const nickname = currentNickname();
  if (!uid || !nickname) return;

  const maxSpellLevel = Math.max(0, ...(profile?.spells ?? []).map((s: any) => s.level ?? 1));
  const maxHeroStars  = Math.max(0, ...(profile?.heroes ?? []).map((h: any) => h.stars  ?? 0));

  try {
    const ref  = doc(fbDb(), "lb", "progression", "scores", uid);
    const snap = await getDoc(ref);
    const ex   = snap.exists() ? snap.data() : null;

    // Skip write if this is not an improvement in any dimension
    if (ex
        && ex.maxLevelIndex >= levelIndex
        && ex.maxSpellLevel >= maxSpellLevel
        && ex.maxHeroStars  >= maxHeroStars) return;

    await setDoc(ref, {
      nickname,
      maxLevelIndex: Math.max(levelIndex,      ex?.maxLevelIndex ?? 0),
      maxLevelId:    levelIndex >= (ex?.maxLevelIndex ?? -1) ? levelId : (ex?.maxLevelId ?? levelId),
      maxSpellLevel: Math.max(maxSpellLevel, ex?.maxSpellLevel ?? 0),
      maxHeroStars:  Math.max(maxHeroStars,  ex?.maxHeroStars  ?? 0),
      updatedAt: serverTimestamp(),
    }, { merge: true });
  } catch { /* best-effort */ }
}
