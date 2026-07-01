import { defineConfig, type Plugin } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { viteStaticCopy } from "vite-plugin-static-copy";
import { existsSync } from "fs";

const REPO_BASE = "/Heroes-Of-Arkanoid/";

/**
 * Rewrites hardcoded absolute asset paths (e.g. /ui/, /atlas/, /_framework/)
 * in the compiled bundle so they include the GitHub Pages sub-path base.
 * Necessary because fetch()/CSS url() string literals are not transformed by Vite's base option.
 */
function fixAbsPathsPlugin(base: string): Plugin {
  const prefixes = ["ui", "art", "atlas", "_framework", "achievements", "hints", "items", "spellicons", "icons", "shkatulka", "levelskill"];
  const basePath = base.endsWith("/") ? base.slice(0, -1) : base;
  // Match all JS string forms and CSS url():
  //   '/ui/'  "/ui/"   — quoted JS strings (src=, style=, return statements)
  //   `/ui/   — backtick template literals (e.g. `/ui/${prefix}.png`)
  //   url(/ui/ — unquoted CSS url()
  const patterns: [RegExp, string][] = prefixes.flatMap((p) => [
    [new RegExp(`(['"])/${p}/`, "g"), `$1${basePath}/${p}/`] as [RegExp, string],
    [new RegExp("(`)" + `/${p}/`, "g"), `$1${basePath}/${p}/`] as [RegExp, string],
    [new RegExp(`(url\\()/${p}/`, "g"), `$1${basePath}/${p}/`] as [RegExp, string],
  ]);

  function fix(str: string): string {
    for (const [pat, rep] of patterns) str = str.replace(pat, rep);
    return str;
  }

  return {
    name: "fix-abs-paths",
    generateBundle(_, bundle) {
      for (const file of Object.values(bundle)) {
        if (file.type === "chunk") file.code = fix(file.code);
        if (file.type === "asset" && typeof file.source === "string")
          file.source = fix(file.source);
      }
    },
  };
}

// Formats the build timestamp in Argentina local time (UTC-3, no DST) so the
// in-game version badge reads in the dev's own clock regardless of CI's TZ.
function buildDateArg(): string {
  const now = new Date();
  const parts = new Intl.DateTimeFormat("sv-SE", {
    timeZone: "America/Argentina/Buenos_Aires",
    year: "numeric", month: "2-digit", day: "2-digit",
    hour: "2-digit", minute: "2-digit", hour12: false,
  }).formatToParts(now);
  const get = (t: string) => parts.find((p) => p.type === t)!.value;
  return `${get("year")}-${get("month")}-${get("day")} ${get("hour")}:${get("minute")} ART`;
}

export default defineConfig({
  base: REPO_BASE,
  define: {
    // Baked in at build time — lets the version badge show exactly which JS bundle is live.
    __BUILD_DATE__: JSON.stringify(buildDateArg()),
  },
  plugins: [
    svelte(),
    // Copy _framework from wasm-dist only when it exists (local builds after build-wasm.ps1).
    // In CI, frontend/public/_framework/ is committed directly so no copy is needed.
    ...(existsSync("../wasm-dist/AppBundle/_framework")
      ? [viteStaticCopy({ targets: [{ src: "../wasm-dist/AppBundle/_framework", dest: "" }] })]
      : []),
    fixAbsPathsPlugin(REPO_BASE),
  ],
  server: { port: 5175, strictPort: true },
  build: {
    rollupOptions: {
      // /_framework/dotnet.js is a runtime URL; must not be bundled or resolved at build time.
      external: (id) => id.includes("_framework/"),
    },
  },
});
