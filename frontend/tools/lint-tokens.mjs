#!/usr/bin/env node
/**
 * lint-tokens — fails if any file under src/ui/ (except theme.ts) contains a
 * raw hex color literal like #fff or #1a2b3c.
 *
 * WHY: All colors must come from CSS custom properties defined in theme.ts so
 * that palette-wide changes require editing one file. Hardcoded hex escapes the
 * token system and silently breaks theming.
 *
 * Run:  node tools/lint-tokens.mjs
 * Used by package.json "lint:tokens" script.
 */
import { readdirSync, readFileSync, statSync } from "fs";
import { join, dirname, relative } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const UI_ROOT = join(__dirname, "..", "src", "ui");

// Matches #rgb, #rrggbb, #rgba, #rrggbbaa — word-boundary ensures we don't
// fire on things like "border-image: url(#id)" in SVGs (rare but possible).
const HEX_RE = /#[0-9a-fA-F]{3,8}\b/;

const violations = [];

function walk(dir) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) { walk(full); continue; }
    if (!name.endsWith(".ts") && !name.endsWith(".css")) continue;
    if (name === "theme.ts") continue; // token definitions — exempt

    const lines = readFileSync(full, "utf8").split("\n");
    lines.forEach((line, i) => {
      const trimmed = line.trimStart();
      if (trimmed.startsWith("//") || trimmed.startsWith("*")) return; // skip comments
      if (HEX_RE.test(line)) {
        violations.push(`  ${relative(UI_ROOT, full)}:${i + 1}  ${trimmed}`);
      }
    });
  }
}

walk(UI_ROOT);

if (violations.length) {
  console.error(
    `\nlint:tokens FAIL — raw hex found in src/ui/ (add a token to theme.ts instead):\n`,
  );
  violations.forEach((v) => console.error(v));
  console.error("");
  process.exit(1);
}

console.log(`lint:tokens OK — no raw hex under src/ui/`);
