import { metaApi, type ItemDef } from "../net/metaApi";
import { unlockAchievement } from "./AchievementsScene";

// ---------------------------------------------------------------------------
// Inventory / Shop scene
// ---------------------------------------------------------------------------
// Layout: header (title + crystal count) → equipped row (3 fixed slots) →
//         item grid (scrollable) → each card: tier-sprite + name + cost + Buy / Equip buttons.
// Mobile portrait, ≥44px touch targets.
// ---------------------------------------------------------------------------

export async function mountInventory(host: HTMLElement) {
  injectStyles();

  const root = document.createElement("div");
  root.id = "inventory-root";
  root.className = "inv-root";
  host.appendChild(root);

  // Header
  const header = document.createElement("div");
  header.className = "inv-header";

  const backBtn = document.createElement("button");
  backBtn.id = "btn-inv-back";
  backBtn.className = "inv-back-btn";
  backBtn.textContent = "← Back";
  backBtn.addEventListener("click", () => { location.href = "/?scene=menu"; });
  header.appendChild(backBtn);

  const title = document.createElement("h1");
  title.className = "inv-title";
  title.textContent = "Items";
  header.appendChild(title);

  const crystalEl = document.createElement("div");
  crystalEl.id = "inv-crystals";
  crystalEl.className = "inv-crystals";
  crystalEl.textContent = "💎 —";
  header.appendChild(crystalEl);

  root.appendChild(header);

  // Equipped row (3 fixed slots, always visible)
  const equippedSection = document.createElement("div");
  equippedSection.className = "inv-equipped-section";

  const equippedLabel = document.createElement("div");
  equippedLabel.className = "inv-section-label";
  equippedLabel.textContent = "EQUIPPED (up to 3)";
  equippedSection.appendChild(equippedLabel);

  const equippedRow = document.createElement("div");
  equippedRow.id = "inv-equipped-row";
  equippedRow.className = "inv-equipped-row";
  for (let i = 0; i < 3; i++) {
    const slot = document.createElement("div");
    slot.className = "inv-equip-slot inv-equip-slot-empty";
    slot.dataset.slot = String(i);
    slot.textContent = String(i + 1);
    equippedRow.appendChild(slot);
  }
  equippedSection.appendChild(equippedRow);
  root.appendChild(equippedSection);

  // Grid label
  const catalogLabel = document.createElement("div");
  catalogLabel.className = "inv-section-label inv-catalog-label";
  catalogLabel.textContent = "ALL ITEMS";
  root.appendChild(catalogLabel);

  // Scrollable item grid
  const grid = document.createElement("div");
  grid.id = "inv-grid";
  grid.className = "inv-grid";
  root.appendChild(grid);

  // Loading state
  grid.textContent = "Loading…";

  // Fetch data and render
  try {
    const data = await metaApi.getItems();
    render(data.items, data.crystals, data.equipped, crystalEl, equippedRow, grid);
  } catch (err) {
    grid.textContent = "Failed to load items.";
    console.error("inventory load error:", err);
  }
}

// ---------------------------------------------------------------------------
// Render / update helpers
// ---------------------------------------------------------------------------

function render(
  items: ItemDef[],
  crystals: number,
  equipped: string[],
  crystalEl: HTMLElement,
  equippedRow: HTMLElement,
  grid: HTMLElement
) {
  updateCrystals(crystalEl, crystals);
  updateEquippedRow(equippedRow, equipped, items);
  buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
}

function updateCrystals(el: HTMLElement, crystals: number) {
  el.textContent = `💎 ${crystals}`;
  el.dataset.crystals = String(crystals);
}

function updateEquippedRow(equippedRow: HTMLElement, equipped: string[], items: ItemDef[]) {
  const slots = equippedRow.querySelectorAll<HTMLElement>("[data-slot]");
  slots.forEach((slot, i) => {
    const itemId = equipped[i];
    const def = itemId ? items.find(it => it.id === itemId) : undefined;
    slot.innerHTML = "";
    if (def) {
      slot.classList.remove("inv-equip-slot-empty");
      slot.classList.add("inv-equip-slot-filled");
      slot.dataset.equipped = def.id;

      const tier = def.ownedTier;
      const img = document.createElement("img");
      img.src = `/items/${def.icon}${tier > 1 ? String(tier) : ""}.png`;
      img.alt = def.name;
      img.className = "inv-slot-sprite";
      img.onerror = () => { img.src = `/items/${def.icon}.png`; img.onerror = null; };
      slot.appendChild(img);

      const label = document.createElement("div");
      label.className = "inv-slot-label";
      label.textContent = def.name;
      slot.appendChild(label);
    } else {
      slot.classList.add("inv-equip-slot-empty");
      slot.classList.remove("inv-equip-slot-filled");
      delete slot.dataset.equipped;
      slot.textContent = String(i + 1);
    }
  });
}

function buildGrid(
  grid: HTMLElement,
  items: ItemDef[],
  equipped: string[],
  crystals: number,
  crystalEl: HTMLElement,
  equippedRow: HTMLElement
) {
  grid.innerHTML = "";

  for (const item of items) {
    const card = buildCard(item, equipped, crystals);
    grid.appendChild(card);

    // Wire buy button
    const buyBtn = card.querySelector<HTMLButtonElement>(".inv-buy-btn");
    if (buyBtn) {
      buyBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        const result = await metaApi.buyItem(item.id);
        if (result.ok) {
          item.ownedTier = result.ownedTier;
          crystals = result.crystals;
          updateCrystals(crystalEl, crystals);
          // Refresh grid
          buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
          updateEquippedRow(equippedRow, equipped, items);
        }
      });
    }

    // Wire equip/unequip button
    const equipBtn = card.querySelector<HTMLButtonElement>(".inv-equip-btn");
    if (equipBtn) {
      equipBtn.addEventListener("click", async (e) => {
        e.stopPropagation();
        const isEquipped = equipped.includes(item.id);
        let result;
        if (isEquipped) {
          result = await metaApi.unequipItem(item.id);
        } else {
          result = await metaApi.equipItem(item.id);
        }
        if (result.ok) {
          equipped.splice(0, equipped.length, ...result.equipped);
          // Update equipped flags on items
          for (const it of items) { it.equipped = equipped.includes(it.id); }
          buildGrid(grid, items, equipped, crystals, crystalEl, equippedRow);
          updateEquippedRow(equippedRow, equipped, items);
          // Unlock achievement on first equip
          if (!isEquipped) unlockAchievement("equip_item").catch(() => {});
        }
      });
    }
  }
}

function buildCard(item: ItemDef, equipped: string[], crystals: number): HTMLElement {
  const card = document.createElement("div");
  card.className = "inv-card";
  card.dataset.itemId = item.id;
  if (item.equipped) card.classList.add("inv-card-equipped");

  // Sprite (tier-appropriate)
  const spriteWrap = document.createElement("div");
  spriteWrap.className = "inv-card-sprite";

  const tier = item.ownedTier;
  if (tier > 0) {
    const img = document.createElement("img");
    const suffix = tier > 1 ? String(tier) : "";
    img.src = `/items/${item.icon}${suffix}.png`;
    img.alt = item.name;
    img.className = "inv-item-sprite";
    img.onerror = () => { img.src = `/items/${item.icon}.png`; img.onerror = null; };
    spriteWrap.appendChild(img);
  } else {
    // Not owned — show locked icon
    const img = document.createElement("img");
    img.src = `/items/LockedItem.png`;
    img.alt = "Locked";
    img.className = "inv-item-sprite inv-item-locked";
    img.onerror = () => { img.style.display = "none"; spriteWrap.textContent = "🔒"; };
    spriteWrap.appendChild(img);
  }
  card.appendChild(spriteWrap);

  // Name + tier badge
  const nameRow = document.createElement("div");
  nameRow.className = "inv-card-name-row";
  const nameEl = document.createElement("div");
  nameEl.className = "inv-card-name";
  nameEl.textContent = item.name;
  nameRow.appendChild(nameEl);

  if (tier > 0) {
    const tierBadge = document.createElement("div");
    tierBadge.className = "inv-tier-badge";
    tierBadge.dataset.tier = String(tier);
    tierBadge.textContent = `T${tier}`;
    nameRow.appendChild(tierBadge);
  }
  card.appendChild(nameRow);

  // Effect description
  const descEl = document.createElement("div");
  descEl.className = "inv-card-desc";
  descEl.textContent = item.description;
  card.appendChild(descEl);

  // Action row: Buy / Equip
  const actions = document.createElement("div");
  actions.className = "inv-card-actions";

  // Buy button (upgrade to next tier if not maxed)
  const nextTier = tier + 1;
  if (tier < item.maxTier) {
    const nextCost = item.cost[nextTier - 1] ?? Infinity;
    const canAfford = crystals >= nextCost;

    const buyBtn = document.createElement("button");
    buyBtn.className = "inv-buy-btn" + (canAfford ? "" : " inv-btn-disabled");
    buyBtn.dataset.cost = String(nextCost);
    buyBtn.textContent = tier === 0 ? `Buy 💎${nextCost}` : `Upgrade 💎${nextCost}`;
    buyBtn.disabled = !canAfford;
    actions.appendChild(buyBtn);
  } else if (tier === item.maxTier) {
    const maxEl = document.createElement("div");
    maxEl.className = "inv-max-badge";
    maxEl.textContent = "MAX";
    actions.appendChild(maxEl);
  }

  // Equip / Unequip button (only if owned)
  if (tier > 0) {
    const isEquipped = equipped.includes(item.id);
    const equipBtn = document.createElement("button");
    equipBtn.className = "inv-equip-btn" + (isEquipped ? " inv-btn-unequip" : "");
    equipBtn.dataset.equipped = String(isEquipped);
    equipBtn.textContent = isEquipped ? "Unequip" : "Equip";
    actions.appendChild(equipBtn);
  }

  card.appendChild(actions);

  return card;
}

// ---------------------------------------------------------------------------
// Styles
// ---------------------------------------------------------------------------

function injectStyles() {
  const id = "inv-styles";
  if (document.getElementById(id)) return;
  const style = document.createElement("style");
  style.id = id;
  style.textContent = `
    .inv-root {
      min-height: 100vh;
      width: 100%;
      background: linear-gradient(180deg, #1a0e06 0%, #0d0808 40%, #050308 100%);
      color: #f0e0b8;
      font-family: sans-serif;
      display: flex;
      flex-direction: column;
      box-sizing: border-box;
      padding: env(safe-area-inset-top, 0px) env(safe-area-inset-right, 0px)
               env(safe-area-inset-bottom, 0px) env(safe-area-inset-left, 0px);
    }

    /* ── Header ── */
    .inv-header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 16px 8px 16px;
      background: rgba(0,0,0,0.55);
      border-bottom: 1px solid rgba(160,120,50,0.3);
      flex-shrink: 0;
    }
    .inv-back-btn {
      background: none;
      border: 1px solid rgba(160,120,50,0.5);
      color: #c8b888;
      border-radius: 6px;
      padding: 8px 12px;
      font-size: 13px;
      cursor: pointer;
      min-height: 44px;
      touch-action: manipulation;
    }
    .inv-back-btn:active { filter: brightness(0.8); }
    .inv-title {
      flex: 1;
      margin: 0;
      font-size: 20px;
      font-weight: 900;
      letter-spacing: 0.08em;
      color: #ffd88a;
      text-shadow: 0 0 12px rgba(255,180,50,0.5);
      text-align: center;
    }
    .inv-crystals {
      font-size: 15px;
      font-weight: 700;
      color: #88ddff;
      min-width: 70px;
      text-align: right;
    }

    /* ── Equipped row ── */
    .inv-equipped-section {
      padding: 10px 16px 6px;
      flex-shrink: 0;
    }
    .inv-section-label {
      font-size: 10px;
      font-weight: 700;
      letter-spacing: 0.14em;
      color: rgba(200,180,100,0.7);
      margin-bottom: 6px;
    }
    .inv-catalog-label {
      padding: 6px 16px 2px;
    }
    .inv-equipped-row {
      display: flex;
      gap: 10px;
      justify-content: center;
    }
    .inv-equip-slot {
      width: 80px;
      height: 80px;
      border-radius: 8px;
      border: 2px solid rgba(160,120,50,0.4);
      background: rgba(20,15,5,0.7);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      color: rgba(200,180,100,0.3);
      flex-shrink: 0;
      gap: 2px;
    }
    .inv-equip-slot-filled {
      border-color: rgba(200,160,60,0.8);
      background: rgba(40,30,10,0.8);
    }
    .inv-slot-sprite {
      width: 44px;
      height: 44px;
      object-fit: contain;
      image-rendering: pixelated;
    }
    .inv-slot-label {
      font-size: 8px;
      color: #c8b888;
      text-align: center;
      line-height: 1.1;
      max-width: 72px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    /* ── Item grid ── */
    .inv-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 10px;
      padding: 8px 16px 24px;
      overflow-y: auto;
      flex: 1;
    }

    /* ── Item card ── */
    .inv-card {
      background: rgba(20,14,6,0.85);
      border: 1px solid rgba(120,90,40,0.45);
      border-radius: 8px;
      padding: 10px 8px 8px;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 5px;
      position: relative;
    }
    .inv-card-equipped {
      border-color: rgba(200,160,60,0.8);
      background: rgba(40,28,8,0.9);
      box-shadow: 0 0 8px rgba(200,150,30,0.35);
    }
    .inv-card-sprite {
      width: 48px;
      height: 48px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .inv-item-sprite {
      width: 44px;
      height: 44px;
      object-fit: contain;
      image-rendering: pixelated;
    }
    .inv-item-locked {
      opacity: 0.35;
      filter: grayscale(0.7);
    }
    .inv-card-name-row {
      display: flex;
      align-items: center;
      gap: 4px;
      width: 100%;
      justify-content: center;
    }
    .inv-card-name {
      font-size: 11px;
      font-weight: 700;
      color: #f0e0b8;
      text-align: center;
      line-height: 1.2;
    }
    .inv-tier-badge {
      font-size: 9px;
      font-weight: 900;
      padding: 1px 4px;
      border-radius: 3px;
      background: rgba(200,150,30,0.3);
      color: #ffd060;
      border: 1px solid rgba(200,150,30,0.5);
    }
    .inv-card-desc {
      font-size: 9px;
      color: rgba(200,180,120,0.7);
      text-align: center;
      line-height: 1.3;
      min-height: 24px;
    }
    .inv-card-actions {
      display: flex;
      flex-direction: column;
      gap: 4px;
      width: 100%;
      margin-top: 2px;
    }
    .inv-buy-btn, .inv-equip-btn {
      width: 100%;
      min-height: 36px;
      border-radius: 6px;
      border: none;
      cursor: pointer;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.04em;
      touch-action: manipulation;
      -webkit-tap-highlight-color: transparent;
    }
    .inv-buy-btn {
      background: linear-gradient(135deg, #664400, #996600);
      color: #ffe090;
      border: 1px solid rgba(200,140,30,0.6);
    }
    .inv-buy-btn:hover:not(:disabled) { filter: brightness(1.15); }
    .inv-buy-btn:active:not(:disabled) { transform: scale(0.96); }
    .inv-buy-btn:disabled, .inv-btn-disabled {
      opacity: 0.4;
      cursor: default;
    }
    .inv-equip-btn {
      background: linear-gradient(135deg, #1a3a1a, #2a5a2a);
      color: #88dd88;
      border: 1px solid rgba(80,180,80,0.4);
    }
    .inv-btn-unequip {
      background: linear-gradient(135deg, #3a1a1a, #5a2a2a);
      color: #dd8888;
      border: 1px solid rgba(180,80,80,0.4);
    }
    .inv-equip-btn:hover { filter: brightness(1.15); }
    .inv-equip-btn:active { transform: scale(0.96); }
    .inv-max-badge {
      font-size: 10px;
      font-weight: 900;
      color: #ffd060;
      text-align: center;
      padding: 4px 0;
      letter-spacing: 0.1em;
    }

    @media (min-width: 480px) {
      .inv-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
  `;
  document.head.appendChild(style);
}
