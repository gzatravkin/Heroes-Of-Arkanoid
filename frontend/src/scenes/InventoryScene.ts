import { metaApi, type ItemDef } from "../net/metaApi";
import { unlockAchievement } from "./AchievementsScene";
import { INVENTORY_STYLES } from "./inventory/inventoryStyles";

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
  crystalEl.innerHTML = `<img src="/ui/Gem.png" style="width:16px;height:16px;vertical-align:middle;image-rendering:pixelated;"> <span id="inv-crystal-count">—</span>`;
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
  const count = el.querySelector("#inv-crystal-count");
  if (count) count.textContent = String(crystals);
  else el.textContent = String(crystals);
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
    // Not owned — show the (dimmed) real item art with a small lock badge, so the
    // shop reads as "items you can unlock" rather than a wall of identical padlocks.
    spriteWrap.style.position = "relative";
    const img = document.createElement("img");
    img.src = `/items/${item.icon}.png`;
    img.alt = item.name;
    img.className = "inv-item-sprite inv-item-locked";
    img.style.cssText += ";filter:grayscale(1) brightness(0.55);";
    spriteWrap.appendChild(img);
    const lock = document.createElement("img");
    lock.src = "/items/LockedItem.png";
    lock.alt = "Locked";
    lock.style.cssText = "position:absolute;right:2px;bottom:2px;width:15px;height:15px;opacity:0.95;";
    lock.onerror = () => { lock.style.display = "none"; };
    spriteWrap.appendChild(lock);
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
    buyBtn.innerHTML = `${tier === 0 ? "Buy" : "Upgrade"} <img src="/ui/Gem.png" style="width:12px;height:12px;vertical-align:middle;image-rendering:pixelated;"> ${nextCost}`;
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
  style.textContent = INVENTORY_STYLES;
  document.head.appendChild(style);
}
