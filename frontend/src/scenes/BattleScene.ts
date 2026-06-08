import { Renderer } from "../render/Renderer";
import { Connection } from "../net/Connection";
import type { Snapshot } from "../net/Connection";
import { attachPaddleInput } from "../input/PaddleInput";
import { installTestHooks } from "../testhooks";
import { Hud } from "../ui/Hud";

const API = "http://localhost:5080";

function css(el: HTMLElement, styles: Record<string, string>) {
  Object.assign(el.style, styles);
}

export function mountBattle(host: HTMLElement, level: string, seed: number, run: string, from = "") {
  const r = new Renderer(host);
  const hud = new Hud(host);
  const conn = new Connection(level, seed, run);

  const fromCampaign = from === "campaign";
  let completeCalled = false;
  let overlayShown = false;

  conn.onSnapshot = (s) => {
    r.draw(s);
    hud.update(s);
    if (fromCampaign && !overlayShown) {
      handleCampaignPhase(s);
    }
  };

  attachPaddleInput(r.app.view as HTMLCanvasElement, conn, () => conn.latest);
  installTestHooks(conn);
  // auto-serve shortly after connect so the ball is live for tests/play
  conn.whenReady(() => setTimeout(() => conn.serve(), 300));

  async function handleCampaignPhase(s: Snapshot) {
    if (s.phase === "Won" && !completeCalled) {
      completeCalled = true;
      overlayShown = true;
      let reward: any = null;
      try {
        const res = await fetch(`${API}/complete?level=${encodeURIComponent(level)}`, { method: "POST" });
        const data = await res.json();
        reward = data.reward;
      } catch (e) {
        console.error("Failed to complete level", e);
      }
      showRewardOverlay(reward);
    } else if (s.phase === "Lost") {
      overlayShown = true;
      showDefeatOverlay();
    }
  }

  function showRewardOverlay(reward: any) {
    const overlay = document.createElement("div");
    overlay.id = "reward-overlay";
    css(overlay, {
      position: "fixed", inset: "0",
      background: "rgba(0,0,0,0.82)",
      display: "flex", flexDirection: "column",
      alignItems: "center", justifyContent: "center",
      zIndex: "1000",
      color: "#e8e8ff",
      fontFamily: "sans-serif",
      gap: "12px",
    });

    const title = document.createElement("div");
    title.textContent = "Victory!";
    css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#ffd700", letterSpacing: "0.1em" });
    overlay.appendChild(title);

    const card = document.createElement("div");
    css(card, {
      background: "#12122a",
      border: "1px solid #334466",
      borderRadius: "12px",
      padding: "24px 36px",
      minWidth: "280px",
      display: "flex",
      flexDirection: "column",
      gap: "8px",
      alignItems: "center",
    });

    if (reward) {
      const expEl = document.createElement("div");
      expEl.id = "reward-exp";
      expEl.textContent = `+${reward.expGained} EXP`;
      css(expEl, { fontSize: "1.2rem", color: "#88aaff" });
      card.appendChild(expEl);

      const pointsEl = document.createElement("div");
      pointsEl.id = "reward-points";
      pointsEl.textContent = `+${reward.pointsGained} Skill Points`;
      css(pointsEl, { fontSize: "1.1rem", color: "#ffcc44" });
      card.appendChild(pointsEl);

      const crystalsEl = document.createElement("div");
      crystalsEl.id = "reward-crystals";
      crystalsEl.textContent = `+${reward.crystalsGained} Crystals`;
      css(crystalsEl, { fontSize: "1.1rem", color: "#44ddff" });
      card.appendChild(crystalsEl);

      if (reward.leveledUp) {
        const lvlUp = document.createElement("div");
        lvlUp.id = "reward-levelup";
        lvlUp.textContent = `Level Up! → Lv ${reward.newLevel}`;
        css(lvlUp, { fontSize: "1.1rem", color: "#ffd700", fontWeight: "700", marginTop: "4px" });
        card.appendChild(lvlUp);
      }

      if (reward.firstClear) {
        const first = document.createElement("div");
        first.textContent = "First Clear!";
        css(first, { fontSize: "0.9rem", color: "#aa88ff", marginTop: "4px" });
        card.appendChild(first);
      }
    } else {
      const msg = document.createElement("div");
      msg.textContent = "Level complete!";
      css(msg, { color: "#88aaff" });
      card.appendChild(msg);
    }

    overlay.appendChild(card);

    const btnContinue = document.createElement("button");
    btnContinue.id = "btn-continue";
    btnContinue.textContent = "Continue";
    css(btnContinue, {
      marginTop: "8px",
      padding: "12px 36px",
      background: "#1a3a2a",
      color: "#55ee88",
      border: "2px solid #33aa66",
      borderRadius: "8px",
      cursor: "pointer",
      fontSize: "16px",
      fontFamily: "sans-serif",
      letterSpacing: "0.05em",
    });
    btnContinue.addEventListener("click", () => {
      location.href = "/?scene=campaign";
    });
    overlay.appendChild(btnContinue);

    document.body.appendChild(overlay);
  }

  function showDefeatOverlay() {
    const overlay = document.createElement("div");
    overlay.id = "defeat-overlay";
    css(overlay, {
      position: "fixed", inset: "0",
      background: "rgba(0,0,0,0.82)",
      display: "flex", flexDirection: "column",
      alignItems: "center", justifyContent: "center",
      zIndex: "1000",
      color: "#e8e8ff",
      fontFamily: "sans-serif",
      gap: "16px",
    });

    const title = document.createElement("div");
    title.textContent = "Defeat";
    css(title, { fontSize: "2.4rem", fontWeight: "700", color: "#ff4444", letterSpacing: "0.1em" });
    overlay.appendChild(title);

    const btnRetry = document.createElement("button");
    btnRetry.id = "btn-retry";
    btnRetry.textContent = "Retry";
    css(btnRetry, {
      padding: "12px 32px",
      background: "#2a1a1a",
      color: "#ff8888",
      border: "2px solid #aa3333",
      borderRadius: "8px",
      cursor: "pointer",
      fontSize: "15px",
      fontFamily: "sans-serif",
    });
    btnRetry.addEventListener("click", () => {
      location.href = `/?scene=battle&level=${encodeURIComponent(level)}&from=campaign`;
    });
    overlay.appendChild(btnRetry);

    const btnMap = document.createElement("button");
    btnMap.id = "btn-map";
    btnMap.textContent = "Map";
    css(btnMap, {
      padding: "12px 32px",
      background: "#1a1a2a",
      color: "#aabbff",
      border: "2px solid #334488",
      borderRadius: "8px",
      cursor: "pointer",
      fontSize: "15px",
      fontFamily: "sans-serif",
    });
    btnMap.addEventListener("click", () => {
      location.href = "/?scene=campaign";
    });
    overlay.appendChild(btnMap);

    document.body.appendChild(overlay);
  }
}
