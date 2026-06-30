<script lang="ts">
  import { tick } from "svelte";
  import { wasmApi as metaApi } from "../net/WasmApi";
  import type { CampaignNode, Profile } from "../net/metaApi";
  import { navigateTo } from "../ui/transition";
  import { log } from "../log";
  import CurrencyBar from "../ui/CurrencyBar.svelte";
  import AuthBadge from "../ui/AuthBadge.svelte";
  import EventBanner from "../ui/EventBanner.svelte";

  // The campaign is a LINEAR chain (economy rework, docs/2026-06-14): nodes render top-to-bottom in
  // list ORDER, one per row, joined by a straight connector to the previous level. No forks, no shops.
  const NW = 104;
  const CT = 6;            // connector thickness
  const ORB_TOP = 6, ORB_SIZE = 64;
  const ORB_CY = ORB_TOP + ORB_SIZE / 2;   // 38
  const ROW_PITCH = 132;   // vertical px per level
  const BASE_LEFT = 28;

  function nodeSrc(id: string, state: "unlocked" | "locked" | "completed"): string {
    const prefix = id.startsWith("hell")    ? "LvlHell"
                 : id.startsWith("caverns") ? "LvlCave"
                 : id.startsWith("village") ? "LvlVillage"
                 : id.startsWith("heaven")  ? "LvlHeaven"
                 : null;
    if (!prefix) return "/art/Mission_Standart.png";
    if (state === "locked")    return `/ui/${prefix}Closed.png`;
    if (state === "completed") return `/ui/${prefix}Selected.png`;
    return `/ui/${prefix}.png`;
  }

  // ── State ────────────────────────────────────────────────────────────────
  let profile         = $state<Profile | null>(null);
  let nodes           = $state<CampaignNode[]>([]);
  let lastUnlockedId  = $state("");
  let prestige        = $state<{ tier: number; canAscend: boolean; rank: number } | null>(null);
  let ascending       = $state(false);

  // Rift banner from URL params
  const _q         = new URLSearchParams(location.search.slice(1));
  const riftId     = _q.get("rift");
  const riftFloors = _q.get("riftFloors") ?? "?";
  const riftName   = _q.get("riftName") ?? "Rift";
  let riftVisible  = $state(false);
  let riftIn       = $state(false);

  // ── Derived map data — linear chain grouped into biome SECTIONS, bosses emphasised ──
  const BIOME_GAP   = 54;     // extra space before each biome banner
  const BANNER_H    = 34;     // biome banner height
  const BIOME_LABEL: Record<string, string> = { hell: "Hell", caverns: "Caverns", village: "Witchland", heaven: "Heaven" };

  interface MapNode {
    node: CampaignNode;
    state: "completed" | "unlocked" | "locked";
    left: number; top: number; isBoss: boolean;
    kicker: string; title: string; src: string;
  }
  interface Conn    { active: boolean; left: number; top: number; w: number; h: number; }
  interface Divider { label: string; biome: string; top: number; }

  let layout = $derived.by(() => {
    const cx = BASE_LEFT + NW / 2;
    const mNodes: MapNode[] = [];
    const conns:  Conn[]    = [];
    const dividers: Divider[] = [];
    let y = 0, prevBiome = "", prevCy = 0;

    for (const node of nodes) {
      if (node.biome !== prevBiome) {
        if (prevBiome !== "") y += BIOME_GAP;
        dividers.push({ label: BIOME_LABEL[node.biome] ?? node.biome, biome: node.biome, top: y });
        y += BANNER_H;
        prevBiome = node.biome;
        prevCy = 0; // no connector spans a biome banner
      }
      const state  = node.completed ? "completed" : node.unlocked ? "unlocked" : "locked";
      const isBoss = node.id.endsWith("-boss");
      const dash   = node.label.indexOf("—");
      const cy     = y + ORB_CY + (isBoss ? 8 : 0);
      if (prevCy > 0)
        conns.push({ active: node.unlocked || node.completed, left: cx - CT / 2, top: prevCy, w: CT, h: cy - prevCy });
      mNodes.push({
        node, state, left: BASE_LEFT, top: y, isBoss,
        kicker: isBoss ? "BOSS" : "",
        title:  dash >= 0 ? node.label.slice(dash + 1).trim() : node.label,
        src: nodeSrc(node.id, state),
      });
      prevCy = cy;
      y += ROW_PITCH + (isBoss ? 18 : 0);
    }
    return { mNodes, conns, dividers, height: y };
  });
  let mapNodes   = $derived(layout.mNodes);
  let connectors = $derived(layout.conns);
  let dividers   = $derived(layout.dividers);
  let innerW = $derived(BASE_LEFT * 2 + NW);
  let innerH = $derived(layout.height);
  let expPct = $derived(profile ? Math.min(100, Math.round((profile.exp / (profile.level * 100)) * 100)) : 0);

  // ── Load ─────────────────────────────────────────────────────────────────
  async function loadAll() {
    const [camp, prof, pres] = await Promise.all([metaApi.getCampaign(), metaApi.getProfile(), metaApi.getPrestige().catch(() => null)]);
    profile = prof;
    nodes   = camp.nodes;
    prestige = pres;
    const playable = camp.nodes.filter(n => n.unlocked);
    const last = playable[playable.length - 1] ?? camp.nodes[0];
    if (last) lastUnlockedId = last.id;
    await tick();
    document.querySelector<HTMLElement>(`[data-level="${lastUnlockedId}"]`)
      ?.scrollIntoView({ block: "center", inline: "nearest", behavior: "smooth" });
  }

  loadAll()
    .then(() => {
      if (riftId) {
        riftVisible = true;
        requestAnimationFrame(() => { riftIn = true; });
        log("rift", "banner-shown", { riftId, riftFloors });
      }
    })
    .catch(console.error);

  // ── Prestige: Ascend into a harder, remixed New Game+ (plan §B.1) ───────────
  async function ascend() {
    if (ascending || !prestige?.canAscend) return;
    if (!confirm("Ascend to the next Prestige loop? Campaign progress resets, but all Cards, Modules, currencies and account level are kept — and the next loop is harder with remixed enemies.")) return;
    ascending = true;
    try { await metaApi.ascend(); await loadAll(); } finally { ascending = false; }
  }

  // ── Rift ──────────────────────────────────────────────────────────────────
  async function descendRift() {
    if (!riftId) return;
    log("rift", "descend", { riftId });
    try {
      await metaApi.startDungeon(riftId);
      navigateTo("/?scene=dungeon");
    } catch (e) {
      log("rift", "descend-failed", { err: String(e) });
    }
  }

  function skipRift() {
    log("rift", "skip", { riftId });
    riftIn = false;
    history.replaceState(null, "", "/?scene=campaign");
    setTimeout(() => { riftVisible = false; }, 300);
  }
</script>

<div id="campaign" class="camp-root">

  <!-- ── Profile bar (decluttered): nav + level/exp on top, a clean coin strip below ── -->
  <div id="profile-bar" class="camp-profile-bar">
    <a href="/?scene=menu" class="camp-back-link">← Menu</a>
    <span id="profile-level" class="camp-profile-level">Lv {profile?.level ?? 1}</span>
    <div class="camp-exp-outer" title="EXP {profile?.exp ?? 0}/{(profile?.level ?? 1) * 100}">
      <div id="profile-exp-fill" class="camp-exp-fill" style="width:{expPct}%"></div>
    </div>
    {#if prestige && prestige.tier > 0}
      <span id="prestige-badge" class="camp-prestige" title="Prestige loop {prestige.tier}">★ P{prestige.tier}</span>
    {/if}
    <div style="flex:1"></div>
    {#if prestige?.canAscend}
      <button id="btn-ascend" class="camp-ascend" onclick={ascend} disabled={ascending} title="Ascend (Prestige)">⬆</button>
    {/if}
    <a href="/?scene=leaderboard" class="camp-lb-btn" title="Leaderboard">🏆</a>
    <AuthBadge />
  </div>
  <div class="camp-coin-strip"><CurrencyBar /></div>
  <EventBanner onClaim={(souls) => { /* devCoins can credit offline; Firestore already stored it */ }} />

  <!-- ── Campaign map ── -->
  <div class="camp-content">
    <div id="campaign-map" class="camp-map">
      <div class="camp-map-inner" style="width:{innerW}px;height:{innerH}px">
        {#each dividers as d}
          <div class="camp-biome-divider biome-{d.biome}" style="top:{d.top}px;width:{innerW}px">
            <span>{d.label}</span>
          </div>
        {/each}
        {#each connectors as conn}
          <div class="camp-connector {conn.active ? 'active' : ''}"
               style="left:{conn.left}px;top:{conn.top}px;width:{conn.w}px;height:{conn.h}px">
          </div>
        {/each}
        {#each mapNodes as mn}
          <button
            data-level={mn.node.id}
            data-state={mn.state}
            class="camp-node camp-node-{mn.state}"
            class:camp-node-boss={mn.isBoss}
            style="position:absolute;left:{mn.left}px;top:{mn.top}px;width:{NW}px"
            onclick={() => {
              if (mn.state === "locked") return;
              navigateTo(`/?scene=battle&level=${mn.node.id}&from=campaign`);
            }}>
            <img src={mn.src} alt={mn.node.label} class="camp-node-img" />
            {#if mn.kicker}<span class="camp-node-kicker-badge">{mn.kicker}</span>{/if}
            <div class="camp-node-label-wrap">
              <span class="camp-node-label">{mn.title}</span>
            </div>
            {#if mn.node.stars > 0}
              <div class="camp-node-stars">{"★".repeat(mn.node.stars)}{"☆".repeat(3 - mn.node.stars)}</div>
            {/if}
          </button>
        {/each}
      </div>
    </div>
  </div>

  <!-- ── Rift banner ── -->
  {#if riftVisible && riftId}
    <div id="rift-banner" class="rift-banner {riftIn ? 'rift-banner-in' : ''}">
      <div class="rift-banner-glyph"></div>
      <div class="rift-banner-text">
        <div class="rift-banner-title">A Rift opens</div>
        <div class="rift-banner-sub">{riftName} · {riftFloors} floors · permadeath · 1 reward / floor</div>
      </div>
      <div class="rift-banner-actions">
        <button id="btn-rift-descend" class="rift-btn rift-btn-go" onclick={descendRift}>Descend</button>
        <button id="btn-rift-skip" class="rift-btn rift-btn-skip" onclick={skipRift}>Skip</button>
      </div>
    </div>
  {/if}

</div>

<style>
  /* ── Root ── */
  .camp-root {
    height: 100cqh;
    background:
      radial-gradient(ellipse at 50% 0%, rgba(60,40,10,0.4) 0%, transparent 60%),
      linear-gradient(180deg, #12080a 0%, #070510 50%, #040308 100%);
    display: flex;
    flex-direction: column;
    overflow: hidden;
    font-family: var(--font-body);
    color: #e8e8ff;
  }

  /* ── Profile bar ── */
  .camp-profile-bar {
    display: flex;
    align-items: center;
    gap: var(--sp-3);
    padding: var(--sp-2) var(--sp-4);
    background: linear-gradient(180deg, rgba(46,34,16,0.96), rgba(24,18,10,0.92));
    border-bottom: 2px solid rgba(180,140,60,0.4);
    flex-shrink: 0;
    flex-wrap: wrap;
    min-height: 52px;
  }
  .camp-profile-level {
    font-weight: 700;
    font-size: var(--fs-section);
    color: var(--gold);
    text-shadow: 0 0 8px rgba(255,200,0,0.6);
    white-space: nowrap;
  }
  .camp-exp-outer {
    position: relative; flex: 0 1 120px; height: 12px;
    background: rgba(0,0,0,0.5); border: 1px solid var(--gold-dim);
    border-radius: 6px; box-sizing: border-box; overflow: hidden;
  }
  /* Coin strip under the bar — keeps the top row uncluttered. */
  .camp-coin-strip {
    display: flex; gap: var(--sp-2); justify-content: center; align-items: center;
    padding: var(--sp-1h) var(--sp-4); flex-shrink: 0;
    background: linear-gradient(180deg, rgba(20,15,8,0.55), transparent);
    border-bottom: 1px solid rgba(180,140,60,0.16);
  }
  .camp-exp-fill {
    position: absolute;
    left: 0; top: 0; bottom: 0;
    background: linear-gradient(180deg, #ffe06a, #d89a2e);
    transition: width var(--dur-slow);
  }
  .camp-coin { font-weight: 800; font-size: var(--fs-body); padding: 3px 9px; border-radius: 16px; white-space: nowrap; }
  .coin-sparks  { color: #ffd56a; background: rgba(190,150,50,0.22); }
  .coin-souls   { color: #6cc0ff; background: rgba(50,110,190,0.22); }
  .coin-insight { color: #e8a64c; background: rgba(150,95,35,0.22); }
  .camp-prestige { font-weight: 800; color: #ff9ddb; font-size: var(--fs-body);
    text-shadow: 0 0 10px rgba(200,80,255,.6); letter-spacing: .04em; }
  .camp-ascend { display: flex; align-items: center; gap: 5px; padding: 6px 12px; min-height: 40px;
    border: 1px solid #c060ff; border-radius: 8px; background: linear-gradient(180deg, rgba(120,40,180,.8), rgba(70,20,120,.85));
    color: #f4e6ff; font-family: var(--font-body); font-weight: 700; font-size: var(--fs-body); cursor: pointer;
    box-shadow: 0 0 12px rgba(180,80,255,.5); transition: filter var(--dur-fast), transform var(--dur-fast); }
  .camp-ascend:hover:not(:disabled) { filter: brightness(1.2); }
  .camp-ascend:active { transform: scale(.96); }
  .camp-ascend:disabled { opacity: .5; }
  .camp-forge-btn {
    display: flex; align-items: center; gap: 5px;
    padding: var(--sp-1) var(--sp-3h);
    background: none; border-style: solid; border-width: 8px 22px;
    border-image: url('/ui/Button1.png') 24 60 24 60 fill stretch;
    color: var(--gold-bright); border-radius: 4px; cursor: pointer;
    font-size: var(--fs-body); font-family: var(--font-body); font-weight: 700;
    min-height: 44px; white-space: nowrap;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .camp-forge-btn:hover  { filter: brightness(1.15); }
  .camp-forge-btn:active { transform: scale(0.96); }
  .camp-forge-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .camp-forge-ico { width: 20px; height: 20px; }

  /* ── Leaderboard button ── */
  .camp-lb-btn {
    display: flex; align-items: center; justify-content: center;
    width: 36px; height: 36px; border-radius: 50%; flex-shrink: 0;
    background: rgba(180,140,60,0.15); border: 1px solid rgba(180,140,60,0.35);
    text-decoration: none; font-size: 18px; line-height: 1;
    transition: background var(--dur-normal), transform var(--dur-fast);
  }
  .camp-lb-btn:hover  { background: rgba(180,140,60,0.3); }
  .camp-lb-btn:active { transform: scale(0.92); }

  /* ── Back link ── */
  .camp-back-link {
    flex-shrink: 0; min-width: 44px; min-height: 44px;
    display: flex; align-items: center; padding: 0 12px;
    text-decoration: none; color: var(--gold-bright); font-weight: 700;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .camp-back-link:hover  { filter: brightness(1.15); }
  .camp-back-link:active { transform: scale(0.96); }
  .camp-back-link:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }

  /* ── Content scroll area ── */
  .camp-content {
    flex: 1;
    overflow: auto;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: thin;
    scrollbar-color: rgba(180,140,60,0.4) transparent;
  }

  /* ── Map (a tall vertical chain; scrolls down, narrow enough to centre in the portrait frame) ── */
  .camp-map {
    width: max-content;
    margin: 0 auto;
    padding: var(--sp-4h) var(--sp-4) var(--sp-6) var(--sp-4);
  }
  .camp-map-inner { position: relative; flex-shrink: 0; }

  /* Connectors */
  .camp-connector {
    position: absolute; border-radius: 3px;
    background: rgba(80,60,20,0.5);
    pointer-events: none;
  }
  .camp-connector.active {
    background: linear-gradient(
      135deg,
      rgba(180,140,60,0.6) 0%,
      rgba(220,180,80,0.95) 50%,
      rgba(180,140,60,0.6) 100%
    );
    box-shadow: 0 0 6px rgba(220,180,60,0.4);
  }

  /* ── Biome section divider ── */
  .camp-biome-divider {
    position: absolute; left: 0; display: flex; align-items: center; justify-content: center;
    height: 34px; pointer-events: none;
  }
  .camp-biome-divider::before, .camp-biome-divider::after {
    content: ""; flex: 1; height: 2px; margin: 0 12px;
    background: linear-gradient(90deg, transparent, rgba(200,160,70,0.45), transparent);
  }
  .camp-biome-divider span {
    font-family: var(--font-display); font-weight: 800; letter-spacing: 0.2em; text-transform: uppercase;
    font-size: var(--fs-small); color: #ffd56a; text-shadow: 0 0 10px rgba(220,160,60,0.55); white-space: nowrap;
  }
  .camp-biome-divider.biome-caverns span { color: #6cc0ff; text-shadow: 0 0 10px rgba(80,160,230,0.5); }
  .camp-biome-divider.biome-village span { color: #e8a64c; text-shadow: 0 0 10px rgba(190,120,40,0.5); }
  .camp-biome-divider.biome-heaven  span { color: #fff2c2; text-shadow: 0 0 12px rgba(255,230,140,0.6); }

  /* ── Boss nodes — larger + ominous ring ── */
  .camp-node-boss .camp-node-img {
    width: 80px; height: 80px;
    filter: drop-shadow(0 0 16px rgba(220,70,40,0.7)) drop-shadow(0 2px 6px rgba(0,0,0,0.85));
  }
  .camp-node-boss.camp-node-unlocked .camp-node-img {
    filter: drop-shadow(0 0 18px rgba(255,90,50,0.9)) drop-shadow(0 0 30px rgba(220,70,40,0.5)) drop-shadow(0 2px 6px rgba(0,0,0,0.85));
  }

  /* Node buttons */
  .camp-node {
    display: flex; flex-direction: column; align-items: center;
    gap: var(--sp-1); padding: var(--sp-1h) var(--sp-1);
    background: transparent; border: none; cursor: pointer;
    flex-shrink: 0;
    transition: transform var(--dur-normal), filter var(--dur-normal);
    -webkit-tap-highlight-color: transparent;
  }
  .camp-node:hover:not(.camp-node-locked) { transform: scale(1.08); filter: brightness(1.15); }
  .camp-node:active:not(.camp-node-locked) { transform: scale(0.96); }
  .camp-node-locked { cursor: default; opacity: 0.7; }
  .camp-node-locked:hover { transform: none; filter: none; }
  .camp-node:not(.camp-node-locked):focus-visible {
    outline: 2px solid var(--gold-bright);
    outline-offset: 3px; border-radius: 4px;
  }
  .camp-node-img {
    width: 64px; height: 64px;
    filter: drop-shadow(0 2px 6px rgba(0,0,0,0.7));
  }
  .camp-node-completed .camp-node-img {
    filter: drop-shadow(0 0 8px rgba(100,220,255,0.8)) drop-shadow(0 2px 6px rgba(0,0,0,0.7));
  }
  /* The actionable "play next" node gets a gold pulse so the eye lands on where to go. */
  .camp-node-unlocked .camp-node-img {
    filter: drop-shadow(0 0 9px rgba(255,190,80,0.85)) drop-shadow(0 2px 6px rgba(0,0,0,0.7));
  }
  @keyframes camp-node-ready {
    0%,100% { filter: drop-shadow(0 0 8px rgba(255,190,80,0.65)) drop-shadow(0 2px 6px rgba(0,0,0,0.7)); }
    50%     { filter: drop-shadow(0 0 16px rgba(255,212,110,1)) drop-shadow(0 0 26px rgba(255,190,80,0.55)) drop-shadow(0 2px 6px rgba(0,0,0,0.7)); }
  }
  @media (prefers-reduced-motion: no-preference) {
    .camp-node-unlocked .camp-node-img { animation: camp-node-ready 1.7s ease-in-out infinite; }
  }

  .camp-node-label-wrap {
    background: none; border-style: solid; border-width: 11px 26px;
    border-image: url('/ui/MissionName.png') 28 70 28 70 fill stretch;
    padding: 3px 14px;
    /* Let the ribbon stretch to fit the title so long names ("Full Furnace", "Statue Ascension")
       don't overflow. A min-width keeps the decorative curled ends OFF short labels ("Demon", "BOSS"),
       which previously overlapped because width:max-content shrank the ribbon onto the art. */
    width: max-content; min-width: 96px; max-width: 240px; text-align: center;
    display: flex; flex-direction: column; align-items: center; gap: 0;
  }
  /* "BOSS" rides ABOVE the ribbon as its own pill so it never collides with the
     ribbon's decorative curled ends (the old in-ribbon two-line stack overlapped). */
  .camp-node-kicker-badge {
    font-size: var(--fs-tiny); font-weight: 900; letter-spacing: 0.18em;
    text-transform: uppercase; color: #ffd9c8;
    background: linear-gradient(180deg, rgba(200,60,40,0.95), rgba(140,30,20,0.95));
    border: 1px solid rgba(255,140,100,0.7); border-radius: 5px;
    padding: 1px 8px; margin-bottom: 3px; line-height: 1.25; white-space: nowrap;
    box-shadow: 0 0 8px rgba(220,70,40,0.6);
  }
  .camp-node-label {
    font-size: var(--fs-small); font-weight: 700; color: var(--text);
    text-shadow: 0 1px 2px rgba(0,0,0,0.95); line-height: 1.2; white-space: nowrap;
  }
  .camp-node-stars {
    font-size: 11px; color: #ffd56a; letter-spacing: 0.06em;
    text-shadow: 0 0 6px rgba(255,200,70,0.55);
    line-height: 1;
  }

  /* ── Rift banner ── */
  .rift-banner {
    position: fixed; left: 50%; top: 64px;
    transform: translate(-50%, -160%);
    width: min(360px, 92cqw); z-index: 200;
    display: flex; align-items: center; gap: var(--sp-3);
    padding: var(--sp-3h) var(--sp-4); box-sizing: border-box;
    background:
      linear-gradient(180deg, rgba(60,10,70,0.96), rgba(30,5,40,0.97)),
      rgba(20,5,30,0.97);
    border: 2px solid #b048e0; border-radius: 12px;
    box-shadow: 0 0 28px rgba(180,70,230,0.55), inset 0 0 30px rgba(120,30,160,0.4);
    color: #f4e6ff; font-family: var(--font-body);
    transition: transform 0.35s cubic-bezier(0.2, 1.1, 0.4, 1);
  }
  .rift-banner-in { transform: translate(-50%, 0); }
  .rift-banner-glyph {
    width: 26px; height: 26px; flex-shrink: 0; border-radius: 50%;
    background: radial-gradient(circle at 38% 35%, #f4d6ff 0%, #c060ff 45%, #5a149a 100%);
    box-shadow: 0 0 14px #c060ff, inset 0 0 6px rgba(255,255,255,0.8);
  }
  @keyframes rift-pulse { 0%,100% { opacity: 0.7; transform: scale(1); } 50% { opacity: 1; transform: scale(1.18); } }
  @media (prefers-reduced-motion: no-preference) {
    .rift-banner-glyph { animation: rift-pulse 1.4s ease-in-out infinite; }
  }
  .rift-banner-text { flex: 1; min-width: 0; }
  .rift-banner-title {
    font-size: var(--fs-section); font-weight: 800; letter-spacing: 0.04em;
    color: #e9b8ff; text-shadow: 0 0 10px rgba(190,90,240,0.7);
  }
  .rift-banner-sub { font-size: var(--fs-tiny); color: #c9a8e0; margin-top: 2px; line-height: 1.3; }
  .rift-banner-actions { display: flex; flex-direction: column; gap: var(--sp-1h); }
  .rift-btn {
    min-width: 78px; min-height: 44px;
    border: none; border-radius: 8px; cursor: pointer;
    font-size: var(--fs-body); font-weight: 700; font-family: var(--font-body);
    touch-action: manipulation; -webkit-tap-highlight-color: transparent;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .rift-btn:active { transform: scale(0.95); }
  .rift-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 8px; }
  .rift-btn-go {
    background: linear-gradient(180deg, #c860ff, #8a28c0);
    color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.6);
    box-shadow: 0 0 12px rgba(190,90,240,0.6);
  }
  .rift-btn-go:hover { filter: brightness(1.15); }
  .rift-btn-skip { background: rgba(40,20,55,0.9); color: #b89ccc; border: 1px solid rgba(150,90,190,0.45); }
  .rift-btn-skip:hover { filter: brightness(1.2); }
</style>
