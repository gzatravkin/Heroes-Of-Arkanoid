import { defineConfig, type Plugin } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";
import { viteStaticCopy } from "vite-plugin-static-copy";

const REPO_BASE = "/Heroes-Of-Arkanoid/";

/**
 * Rewrites hardcoded absolute asset paths (e.g. /ui/, /atlas/, /_framework/)
 * in the compiled bundle so they include the GitHub Pages sub-path base.
 * Necessary because fetch()/CSS url() string literals are not transformed by Vite's base option.
 */
function fixAbsPathsPlugin(base: string): Plugin {
  const prefixes = ["ui", "art", "atlas", "_framework", "achievements", "hints", "items", "spellicons", "icons", "shkatulka", "levelskill"];
  const basePath = base.endsWith("/") ? base.slice(0, -1) : base;
  // Match both quoted ('/ui/') and unquoted url(/ui/) CSS forms
  const patterns: [RegExp, string][] = prefixes.flatMap((p) => [
    [new RegExp(`(['"])/${p}/`, "g"), `$1${basePath}/${p}/`] as [RegExp, string],
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

export default defineConfig({
  base: REPO_BASE,
  plugins: [
    svelte(),
    // Copy _framework from wasm-dist for local builds (GitHub Actions uses public/_framework directly).
    viteStaticCopy({
      targets: [
        {
          src: "../wasm-dist/AppBundle/_framework",
          dest: "",
        },
      ],
    }),
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
