import {
  signInAnonymously, linkWithPopup, GoogleAuthProvider,
  onAuthStateChanged, type User,
} from "firebase/auth";
import { doc, setDoc, getDoc, serverTimestamp } from "firebase/firestore";
import { fbAuth, fbDb, isFirebaseConfigured } from "./firebase";

// Google Play Games Services scope — gives access to GPGS REST APIs
const GPGS_SCOPE = "https://www.googleapis.com/auth/games";

let _uid:         string | null = null;
let _nickname:    string | null = null;
let _accessToken: string | null = null;

export function currentUid():         string | null { return _uid; }
export function currentNickname():    string | null { return _nickname; }
export function currentAccessToken(): string | null { return _accessToken; }

export function isLinkedGoogle(): boolean {
  if (!isFirebaseConfigured()) return false;
  return !!fbAuth().currentUser?.providerData.find(p => p.providerId === "google.com");
}

function makeNickname(uid: string): string {
  return `Hero#${uid.slice(0, 4).toUpperCase()}`;
}

async function ensureNickname(user: User): Promise<void> {
  try {
    const snap = await getDoc(doc(fbDb(), "users", user.uid));
    if (snap.exists() && snap.data().nickname) {
      _nickname = snap.data().nickname as string;
    } else {
      _nickname = makeNickname(user.uid);
      await setDoc(
        doc(fbDb(), "users", user.uid),
        { nickname: _nickname, linkedGoogle: false, createdAt: serverTimestamp() },
        { merge: true },
      );
    }
  } catch {
    _nickname = makeNickname(user.uid);
  }
}

/** Call once at app start. Resolves when the auth state is known. */
export function initAuth(): Promise<void> {
  if (!isFirebaseConfigured()) return Promise.resolve();
  return new Promise((resolve) => {
    const unsub = onAuthStateChanged(fbAuth(), async (user) => {
      unsub(); // one-shot — we only need the initial state here
      if (user) {
        _uid = user.uid;
        await ensureNickname(user);
      } else {
        try {
          const cred = await signInAnonymously(fbAuth());
          _uid = cred.user.uid;
          await ensureNickname(cred.user);
        } catch (e) {
          console.warn("[Auth] anonymous sign-in failed", e);
        }
      }
      resolve();
    }, () => resolve());
  });
}

/** Subscribe to future auth-state changes (for reactive UI). Returns an unsubscribe fn. */
export function subscribeAuth(cb: (uid: string | null, nickname: string | null, linked: boolean) => void): () => void {
  if (!isFirebaseConfigured()) return () => {};
  return onAuthStateChanged(fbAuth(), (user) => {
    cb(user?.uid ?? null, _nickname, isLinkedGoogle());
  });
}

/** Update the player's display nickname locally and in Firestore. */
export async function updateNickname(name: string): Promise<{ ok: boolean; error?: string }> {
  if (!isFirebaseConfigured()) return { ok: false, error: "not_configured" };
  const uid = _uid ?? fbAuth().currentUser?.uid;
  if (!uid) return { ok: false, error: "not_signed_in" };
  const trimmed = name.trim().slice(0, 24);
  if (!trimmed) return { ok: false, error: "empty" };
  try {
    await setDoc(doc(fbDb(), "users", uid), { nickname: trimmed }, { merge: true });
    _nickname = trimmed;
    return { ok: true };
  } catch (e: any) {
    return { ok: false, error: String(e?.message ?? e) };
  }
}

/** Upgrade an anonymous session to Google. Adds the GPGS scope so the access
 *  token can be used with the Play Games REST API. */
export async function linkGoogle(): Promise<{ ok: boolean; error?: string }> {
  if (!isFirebaseConfigured()) return { ok: false, error: "not_configured" };
  const current = fbAuth().currentUser;
  if (!current) return { ok: false, error: "not_signed_in" };
  try {
    const provider = new GoogleAuthProvider();
    provider.addScope(GPGS_SCOPE);
    const result  = await linkWithPopup(current, provider);
    _uid          = result.user.uid;
    const oauthCred = GoogleAuthProvider.credentialFromResult(result);
    _accessToken  = oauthCred?.accessToken ?? null;
    await setDoc(doc(fbDb(), "users", _uid), { linkedGoogle: true }, { merge: true });
    return { ok: true };
  } catch (e: any) {
    if (e?.code === "auth/credential-already-in-use") {
      return { ok: false, error: "already_linked" };
    }
    console.warn("[Auth] linkGoogle failed", e);
    return { ok: false, error: String(e?.message ?? e) };
  }
}
