import { metaApi } from "../net/metaApi";
import { css } from "./battle/overlays";

// Icon filenames in public/art/ that correspond to the icon keys from the backend.
const ICON_FILES: Record<string, string> = {
  FireHeroBall: "/art/FireHeroBall.png",
  HPFull:       "/art/HPFull.png",
  FireTurretIco:"/art/FireTurretIco.png",
  MPFull:       "/art/MPFull.png",
};

function iconSrc(key: string): string {
  return ICON_FILES[key] ?? "/art/ItemGem.png";
}

export function mountCharacters(host: HTMLElement) {
  const root = document.createElement("div");
  root.id = "character-scene";
  css(root, {
    color: "#e8e8ff",
    fontFamily: "sans-serif",
    textAlign: "center",
    paddingTop: "8vh",
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: "0",
    minHeight: "100vh",
    background: "#0b0b12",
  });

  const h1 = document.createElement("h1");
  h1.textContent = "Choose Character";
  css(h1, { margin: "0 0 6px 0", fontSize: "2rem", letterSpacing: "0.08em" });
  root.appendChild(h1);

  const sub = document.createElement("p");
  sub.textContent = "Your passive ability applies for every level.";
  css(sub, { margin: "0 0 28px 0", color: "#8899cc", fontSize: "0.9rem", letterSpacing: "0.04em" });
  root.appendChild(sub);

  const list = document.createElement("div");
  list.id = "character-list";
  css(list, {
    display: "flex",
    flexWrap: "wrap",
    gap: "16px",
    justifyContent: "center",
    maxWidth: "680px",
  });
  root.appendChild(list);

  // Back link
  const back = document.createElement("a");
  back.href = "/?scene=menu";
  back.textContent = "← Back to Menu";
  css(back, {
    marginTop: "32px",
    color: "#6677cc",
    fontSize: "0.9rem",
    textDecoration: "underline",
    cursor: "pointer",
  });
  root.appendChild(back);

  host.appendChild(root);

  async function render() {
    const data = await metaApi.getCharacters();
    // If unlocked is empty, treat ALL catalog characters as selectable.
    const selectable = data.unlocked.length === 0
      ? data.characters.map(c => c.id)
      : data.unlocked;

    list.innerHTML = "";
    list.setAttribute("data-selected", data.selected ?? "");

    for (const char of data.characters) {
      const isSelected = char.id === data.selected;
      const isSelectable = selectable.includes(char.id);

      const card = document.createElement("div");
      card.setAttribute("data-character", char.id);
      if (isSelected) card.classList.add("selected");
      css(card, {
        background: isSelected ? "#1e1a40" : "#12122a",
        border: isSelected ? "2px solid #aa88ff" : "2px solid #334466",
        borderRadius: "12px",
        padding: "20px 24px",
        width: "140px",
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: "10px",
        cursor: isSelectable ? "pointer" : "default",
        opacity: isSelectable ? "1" : "0.45",
        transition: "background 0.15s, border-color 0.15s, transform 0.1s",
        position: "relative",
      });

      if (isSelected) {
        const badge = document.createElement("div");
        badge.textContent = "Selected";
        css(badge, {
          position: "absolute",
          top: "6px",
          right: "8px",
          fontSize: "0.65rem",
          color: "#aa88ff",
          letterSpacing: "0.06em",
          fontWeight: "700",
        });
        card.appendChild(badge);
      }

      const icon = document.createElement("img");
      icon.src = iconSrc(char.icon);
      css(icon, { width: "48px", height: "48px", imageRendering: "pixelated" });
      card.appendChild(icon);

      const nameEl = document.createElement("div");
      nameEl.textContent = char.name;
      css(nameEl, { fontSize: "15px", fontWeight: "700", color: isSelected ? "#cc99ff" : "#aabbff" });
      card.appendChild(nameEl);

      const passiveEl = document.createElement("div");
      passiveEl.textContent = char.passive;
      css(passiveEl, { fontSize: "11px", color: "#778899", lineHeight: "1.4", textAlign: "center" });
      card.appendChild(passiveEl);

      if (isSelectable) {
        card.addEventListener("mouseenter", () => {
          if (!card.classList.contains("selected")) {
            card.style.background = "#1a1a44";
            card.style.borderColor = "#5566aa";
          }
          card.style.transform = "scale(1.04)";
        });
        card.addEventListener("mouseleave", () => {
          if (!card.classList.contains("selected")) {
            card.style.background = "#12122a";
            card.style.borderColor = "#334466";
          }
          card.style.transform = "scale(1)";
        });
        card.addEventListener("click", async () => {
          if (char.id === data.selected) return; // already selected
          await metaApi.selectCharacter(char.id);
          await render(); // re-render with new selection
        });
      }

      list.appendChild(card);
    }
  }

  render().catch(console.error);
}
