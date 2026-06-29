import { mount, unmount } from "svelte";
import { wasmApi as metaApi } from "../net/WasmApi";
import TutorialOverlay from "./TutorialOverlay.svelte";

export async function maybeShowTutorial(host: HTMLElement, force = false): Promise<void> {
  if (!force) {
    try {
      const profile = await metaApi.getProfile();
      if (profile.tutorialSeen) return;
    } catch {
      if (localStorage.getItem("arkanoid_tutorial_seen") === "1") return;
    }
  }
  return new Promise<void>((resolve) => {
    showTutorial(host, () => {
      localStorage.setItem("arkanoid_tutorial_seen", "1");
      metaApi.markTutorialSeen().catch(() => {/* non-fatal */});
      resolve();
    });
  });
}

export function showTutorial(host: HTMLElement, onDone?: () => void) {
  let instance: ReturnType<typeof mount> | null = null;
  instance = mount(TutorialOverlay, {
    target: host,
    props: {
      onDone: () => {
        if (instance) { unmount(instance); instance = null; }
        onDone?.();
      },
    },
  });
}
