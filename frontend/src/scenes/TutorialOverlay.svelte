<script lang="ts">
  interface TutorialSlide {
    screenSrc: string;
    title: string;
    caption: string;
    icons: Array<{ src: string; label: string }>;
  }

  const SLIDES: TutorialSlide[] = [
    {
      screenSrc: "/hints/HintsScreen1.png",
      title: "Move & Serve",
      caption: "Drag your paddle left/right to deflect the ball. Tap the screen to serve at the start.",
      icons: [{ src: "/hints/EducationHeroIco.png", label: "Your hero paddle" }],
    },
    {
      screenSrc: "/hints/HintsScreen2.png",
      title: "Spells & Mana",
      caption: "Tap hotbar slots (Q/E/W/R) to cast spells. Each spell costs mana — watch the blue bar!",
      icons: [
        { src: "/hints/EducationSpellBunner.png", label: "Spell banner" },
        { src: "/hints/EducationLife.png",         label: "Life indicator" },
      ],
    },
    {
      screenSrc: "/hints/HintsScreen3.png",
      title: "Bonuses & Boss",
      caption: "Catch falling bonuses to power up. Clear all blocks to meet the boss — and defeat it!",
      icons: [
        { src: "/hints/EducationGem.png",    label: "Gem bonus" },
        { src: "/hints/EducationChance.png", label: "Chance bonus" },
      ],
    },
  ];

  let { onDone = () => {} }: { onDone?: () => void } = $props();

  let currentSlide = $state(0);
  const slide = $derived(SLIDES[currentSlide]);
  const isLast = $derived(currentSlide === SLIDES.length - 1);

  function next() {
    if (isLast) { onDone(); } else { currentSlide++; }
  }
  function back() { currentSlide = Math.max(0, currentSlide - 1); }
  function skip() { onDone(); }
</script>

<div id="tutorial-overlay" class="tut-overlay" role="dialog" aria-modal="true" aria-labelledby="tutorial-title">
  <img class="tut-screen-img" src={SLIDES[currentSlide].screenSrc} alt={SLIDES[currentSlide].title} />

  <div class="tut-panel">
    <h2 id="tutorial-title" class="tut-title">{SLIDES[currentSlide].title}</h2>
    <p class="tut-caption">{SLIDES[currentSlide].caption}</p>

    {#if SLIDES[currentSlide].icons.length > 0}
      <div class="tut-icon-row">
        {#each SLIDES[currentSlide].icons as ic}
          <div class="tut-icon-wrap">
            <img class="tut-icon-img" src={ic.src} alt={ic.label} />
            <span class="tut-icon-label">{ic.label}</span>
          </div>
        {/each}
      </div>
    {/if}

    <div class="tut-dots">
      {#each SLIDES as _, i}
        <span class="tut-dot {i === currentSlide ? 'active' : ''}"></span>
      {/each}
    </div>

    <div class="tut-btn-row">
      {#if currentSlide > 0}
        <button class="tut-btn tut-btn-secondary" onclick={back}>← Back</button>
      {:else}
        <div style="flex:1"></div>
      {/if}
      <button id={isLast ? 'tut-btn-done' : 'tut-btn-next'}
              class="tut-btn {isLast ? 'tut-btn-done' : 'tut-btn-primary'}"
              onclick={next}>
        {isLast ? 'Got it!' : 'Next →'}
      </button>
    </div>
  </div>

  <button class="tut-skip" onclick={skip}>Skip tutorial</button>
</div>

<style>
  .tut-overlay {
    position: absolute; inset: 0;
    background: rgba(0,0,0,0.88);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    z-index: 2000;
    font-family: var(--font-body);
    padding: max(env(safe-area-inset-top,0px),12px) 16px max(env(safe-area-inset-bottom,0px),12px);
    box-sizing: border-box;
    gap: var(--sp-3);
    overflow: hidden;
  }
  .tut-screen-img {
    position: absolute; inset: 0;
    width: 100%; height: 100%;
    object-fit: cover;
    opacity: 0.22;
    pointer-events: none;
    image-rendering: pixelated;
  }
  .tut-panel {
    position: relative; z-index: 1;
    background: url('/ui/LvlUpInterfacePanel.png') no-repeat center / cover, rgba(8,6,20,0.96);
    border: 2px solid var(--gold-dim);
    border-radius: 16px;
    padding: 28px 24px 20px;
    width: min(340px, 92cqw);
    display: flex; flex-direction: column; align-items: center;
    gap: var(--sp-3);
    box-shadow: 0 8px 40px rgba(0,0,0,0.8), inset 0 0 40px rgba(10,5,30,0.6);
  }
  .tut-panel::before, .tut-panel::after {
    content: '';
    position: absolute; left: 0; right: 0; height: 16px;
    background: url('/ui/LvlUpInterfaceTopBottomPanel.png') repeat-x center / auto 100%;
  }
  .tut-panel::before { top: 0; border-radius: 14px 14px 0 0; }
  .tut-panel::after  { bottom: 0; border-radius: 0 0 14px 14px; }
  .tut-title {
    margin: 0;
    font-family: var(--font-display);
    font-size: var(--fs-title);
    font-weight: 700;
    color: var(--gold-bright);
    letter-spacing: 0.07em;
    text-shadow: 0 0 16px rgba(255,200,50,0.5), 0 2px 4px rgba(0,0,0,0.9);
    text-align: center;
  }
  .tut-caption {
    margin: 0;
    font-size: var(--fs-section);
    color: var(--text-dim);
    line-height: 1.55;
    text-align: center;
    max-width: 300px;
  }
  .tut-icon-row { display: flex; gap: var(--sp-4h); justify-content: center; flex-wrap: wrap; }
  .tut-icon-wrap { display: flex; flex-direction: column; align-items: center; gap: var(--sp-1h); }
  .tut-icon-img { width: 56px; height: 56px; image-rendering: pixelated; filter: drop-shadow(0 2px 6px rgba(0,0,0,0.8)); }
  .tut-icon-label { font-size: var(--fs-small); color: var(--text-dim); text-align: center; }
  .tut-dots { display: flex; gap: var(--sp-2); justify-content: center; align-items: center; min-height: 44px; padding: 18px 0; }
  .tut-dot {
    width: 8px; height: 8px;
    border-radius: 50%;
    background: rgba(200,180,100,0.3);
    border: 1px solid rgba(200,180,100,0.5);
    transition: background var(--dur-normal);
  }
  .tut-dot.active { background: var(--gold-bright); box-shadow: 0 0 6px rgba(255,210,0,0.6); }
  .tut-btn-row { display: flex; width: 100%; justify-content: space-between; align-items: center; gap: var(--sp-3); margin-top: var(--sp-1); }
  .tut-btn {
    height: 48px; min-width: 100px;
    background-image: url('/ui/LvlUpBtnInterface.png');
    background-size: 100% 100%;
    border: none; outline: none;
    cursor: pointer;
    font-family: var(--font-body);
    font-size: var(--fs-section);
    font-weight: 700;
    letter-spacing: 0.04em;
    color: var(--text);
    text-shadow: 0 1px 3px rgba(0,0,0,0.9);
    -webkit-tap-highlight-color: transparent;
    touch-action: manipulation;
    transition: filter var(--dur-normal), transform var(--dur-fast);
  }
  .tut-btn:hover  { filter: brightness(1.15); }
  .tut-btn:active { transform: scale(0.97); filter: brightness(0.9); }
  .tut-btn:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
  .tut-btn-secondary { opacity: 0.75; min-width: 80px; }
  .tut-btn-done { filter: brightness(1.05); }
  .tut-skip {
    position: relative; z-index: 1;
    background: none; border: none;
    color: rgba(200,180,140,0.55);
    font-size: var(--fs-caption);
    cursor: pointer;
    min-height: 44px; padding: 0 8px;
    display: flex; align-items: center;
    font-family: var(--font-body);
    -webkit-tap-highlight-color: transparent;
  }
  .tut-skip:hover { color: rgba(200,180,140,0.85); }
  .tut-skip:focus-visible { outline: 2px solid var(--gold-bright); outline-offset: 3px; border-radius: 4px; }
</style>
