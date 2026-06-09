/**
 * atlas-smoke.spec.ts
 * Verifies that the atlas loads and key frames + animations resolve to real textures.
 * Runs in the battle scene (which calls loadAtlas() before mounting).
 * This spec is removed after P0 verification is complete — it's purely a DoD check.
 */
import { test, expect } from "@playwright/test";

test("atlas: tex(hell/StandartHell) returns a valid texture after loadAtlas", async ({ page }) => {
  // Load battle scene — this ensures loadAtlas() completes before mounting
  await page.goto("/?scene=battle&level=hell-1&seed=1&run=atlas-smoke");

  // Wait for loadAtlas() to fully complete — signalled by __atlas.ready.
  await page.waitForFunction(
    () => !!(window as any).__atlas?.ready,
    { timeout: 30000 }
  );

  const result = await page.evaluate(() => {
    const atlas = (window as any).__atlas;
    const frameKeys: string[] = atlas.frames();
    const animKeys: string[] = atlas.anims();

    const hellFrame = atlas.tex("hell/StandartHell");
    const phonexAnim: unknown[] = atlas.anim("firemage/spell_phonex/phoenixdeathanimpic");
    const bgTex = atlas.tex("fons/1Hell");

    return {
      totalFrames: frameKeys.length,
      totalAnims: animKeys.length,
      hellFrameValid: hellFrame && hellFrame !== (window as any).PIXI?.Texture?.WHITE,
      hellFrameWidth: hellFrame?.width ?? 0,
      phonexAnimLength: phonexAnim.length,
      bgValid: bgTex && bgTex !== (window as any).PIXI?.Texture?.WHITE,
      bgWidth: bgTex?.width ?? 0,
    };
  });

  console.log("Atlas smoke result:", result);

  expect(result.totalFrames).toBeGreaterThanOrEqual(750);
  expect(result.totalAnims).toBeGreaterThanOrEqual(40);
  expect(result.hellFrameWidth).toBeGreaterThan(0);
  expect(result.phonexAnimLength).toBe(18);
  expect(result.bgWidth).toBeGreaterThan(0);
});
