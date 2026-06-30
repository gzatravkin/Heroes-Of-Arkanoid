import {
  collection, query, where, orderBy, limit, getDocs, doc, getDoc,
} from "firebase/firestore";
import { fbDb, isFirebaseConfigured } from "./firebase";
import { currentUid } from "./FirebaseAuth";

export type BoardWindow = "day" | "week" | "month";
export type BoardType   = "souls" | "progression";

export interface BoardRow {
  uid:           string;
  nickname:      string;
  rank:          number;
  score:         number;        // souls (time boards) or maxLevelIndex (progression)
  maxSpellLevel?: number;
  maxHeroStars?:  number;
  maxLevelId?:    string;
}

export interface BoardResult {
  rows:       BoardRow[];
  playerRank: number | null;   // null = not on board; -1 = on board but outside top 100
}

// ── Period helpers (must match FirestoreSync exactly) ─────────────────────────

function dayStartMs(d = new Date()): number {
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate());
}

function weekStartMs(d = new Date()): number {
  const utc = dayStartMs(d);
  return utc - ((new Date(utc).getUTCDay() + 6) % 7) * 86_400_000;
}

function monthStartMs(d = new Date()): number {
  return Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), 1);
}

// ── Public API ────────────────────────────────────────────────────────────────

export async function getBoard(type: BoardType, window?: BoardWindow): Promise<BoardResult> {
  if (!isFirebaseConfigured()) return { rows: [], playerRank: null };
  try {
    return type === "souls" ? getSoulsBoard(window ?? "week") : getProgressionBoard();
  } catch (e) {
    console.warn("[Board] fetch failed", e);
    return { rows: [], playerRank: null };
  }
}

// ── Internal ─────────────────────────────────────────────────────────────────

async function getSoulsBoard(window: BoardWindow): Promise<BoardResult> {
  const col  = `souls_${window}` as const;
  const periodStart = window === "day"  ? dayStartMs()
                    : window === "week" ? weekStartMs()
                    : monthStartMs();

  const q    = query(
    collection(fbDb(), "lb", col, "scores"),
    where("periodStart", "==", periodStart),
    orderBy("souls", "desc"),
    limit(100),
  );
  const snap = await getDocs(q);
  const uid  = currentUid();
  let playerRank: number | null = null;

  const rows: BoardRow[] = snap.docs.map((d, i) => {
    const data = d.data();
    const rank = i + 1;
    if (d.id === uid) playerRank = rank;
    return { uid: d.id, nickname: data.nickname, score: data.souls, rank };
  });

  if (playerRank === null && uid) {
    const own = await getDoc(doc(fbDb(), "lb", col, "scores", uid));
    if (own.exists() && own.data().periodStart === periodStart) playerRank = -1;
  }
  return { rows, playerRank };
}

async function getProgressionBoard(): Promise<BoardResult> {
  const q    = query(
    collection(fbDb(), "lb", "progression", "scores"),
    orderBy("maxLevelIndex", "desc"),
    orderBy("maxSpellLevel", "desc"),
    orderBy("maxHeroStars",  "desc"),
    limit(100),
  );
  const snap = await getDocs(q);
  const uid  = currentUid();
  let playerRank: number | null = null;

  const rows: BoardRow[] = snap.docs.map((d, i) => {
    const data = d.data();
    const rank = i + 1;
    if (d.id === uid) playerRank = rank;
    return {
      uid:           d.id,
      nickname:      data.nickname,
      rank,
      score:         data.maxLevelIndex ?? 0,
      maxSpellLevel: data.maxSpellLevel,
      maxHeroStars:  data.maxHeroStars,
      maxLevelId:    data.maxLevelId,
    };
  });
  return { rows, playerRank };
}
