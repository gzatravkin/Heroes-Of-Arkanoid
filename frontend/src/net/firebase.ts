import { initializeApp, type FirebaseApp } from "firebase/app";
import { getAuth, type Auth } from "firebase/auth";
import { getFirestore, type Firestore } from "firebase/firestore";

const cfg = {
  apiKey:            import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain:        import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId:         import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket:     import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId:             import.meta.env.VITE_FIREBASE_APP_ID,
};

let _app:  FirebaseApp | null = null;
let _auth: Auth        | null = null;
let _db:   Firestore   | null = null;

export function isFirebaseConfigured(): boolean {
  return typeof cfg.apiKey === "string" && cfg.apiKey.length > 8;
}

export function getFirebaseApp(): FirebaseApp {
  if (!_app) _app = initializeApp(cfg);
  return _app;
}

export function fbAuth(): Auth {
  if (!_auth) _auth = getAuth(getFirebaseApp());
  return _auth;
}

export function fbDb(): Firestore {
  if (!_db) _db = getFirestore(getFirebaseApp());
  return _db;
}
