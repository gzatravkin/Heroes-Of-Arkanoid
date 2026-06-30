import {
  doc, getDoc, setDoc, onSnapshot, runTransaction, serverTimestamp,
  type Unsubscribe,
} from "firebase/firestore";
import { fbDb, isFirebaseConfigured } from "./firebase";
import { currentUid } from "./FirebaseAuth";

export interface ActiveEvent {
  id:           string;
  name:         string;
  description:  string;
  targetCount:  number;
  currentCount: number;
  rewardSouls:  number;
  endsAt:       number;   // epoch ms
  status:       "active" | "complete";
}

export function subscribeEvent(cb: (ev: ActiveEvent | null) => void): Unsubscribe {
  if (!isFirebaseConfigured()) { cb(null); return () => {}; }
  return onSnapshot(
    doc(fbDb(), "events", "active"),
    (snap) => {
      if (!snap.exists()) { cb(null); return; }
      const d = snap.data();
      cb({
        id:           snap.id,
        name:         d.name        ?? "Community Event",
        description:  d.description ?? "",
        targetCount:  d.targetCount ?? 0,
        currentCount: d.currentCount ?? 0,
        rewardSouls:  d.rewardSouls  ?? 0,
        endsAt:       d.endsAt?.toMillis?.() ?? 0,
        status:       d.status       ?? "active",
      });
    },
    () => cb(null),
  );
}

/** Increment the community counter by 1 and mark the player as a participant. */
export async function recordEventClear(): Promise<void> {
  if (!isFirebaseConfigured()) return;
  const uid = currentUid();
  if (!uid) return;
  try {
    const eventRef = doc(fbDb(), "events", "active");
    const partRef  = doc(fbDb(), "events", "active", "participants", uid);
    await runTransaction(fbDb(), async (tx) => {
      const ev = await tx.get(eventRef);
      if (!ev.exists() || ev.data().status !== "active") return;
      const next = (ev.data().currentCount ?? 0) + 1;
      const part = await tx.get(partRef);
      tx.update(eventRef, {
        currentCount: next,
        status: next >= ev.data().targetCount ? "complete" : "active",
      });
      tx.set(partRef, { clearedCount: (part.data()?.clearedCount ?? 0) + 1 }, { merge: true });
    });
  } catch { /* best-effort */ }
}

/** Claim the event reward. Returns Souls granted (0 if already claimed or event gone). */
export async function claimEventReward(eventId: string): Promise<number> {
  if (!isFirebaseConfigured()) return 0;
  const uid = currentUid();
  if (!uid) return 0;
  try {
    const partRef = doc(fbDb(), "events", eventId, "participants", uid);
    const partSnap = await getDoc(partRef);
    if (partSnap.exists() && partSnap.data().rewarded) return 0;

    const evSnap = await getDoc(doc(fbDb(), "events", eventId));
    if (!evSnap.exists() || evSnap.data().status !== "complete") return 0;

    const souls = evSnap.data().rewardSouls ?? 0;
    await setDoc(partRef, { rewarded: true, rewardedAt: serverTimestamp() }, { merge: true });
    return souls;
  } catch { return 0; }
}

export async function hasParticipated(eventId: string): Promise<boolean> {
  if (!isFirebaseConfigured()) return false;
  const uid = currentUid();
  if (!uid) return false;
  try {
    const snap = await getDoc(doc(fbDb(), "events", eventId, "participants", uid));
    return snap.exists() && (snap.data().clearedCount ?? 0) > 0;
  } catch { return false; }
}
