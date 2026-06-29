import type { Snapshot } from "../../net/Connection";
import { navigateTo } from "../../ui/transition";

/**
 * Weekly Trial post-battle flow (plan §A.3). The score is computed + submitted SERVER-side at battle end
 * (the client never sends a number), so here we just return to the league once the run resolves.
 */
export function createTrialFlow() {
  let done = false;
  async function handlePhase(s: Snapshot): Promise<boolean> {
    if (done) return true;
    if (s.phase === "Won" || s.phase === "Lost") {
      done = true;
      // Give the server a beat to persist the score, then show the (updated) ladder.
      setTimeout(() => navigateTo("/?scene=league"), 1200);
      return true;
    }
    return false;
  }
  return { handlePhase };
}
