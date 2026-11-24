#!/usr/bin/env node
// framework-ui-check-colors [folder…]: fails when a file under the folders (src by default) writes a colour
// instead of a theme variable. Colours are set only in a theme.css (README.md, "Theme").
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

let finder;
try {
  finder = await import("../dist/theme/rawColors.js");
} catch {
  console.error("framework-ui-check-colors: the package isn't built. In a clone of tl-framework, run npm run build first.");
  process.exit(2);
}
const { findRawColors, isThemeFile, RAW_COLOR_ALLOWED } = finder;

const SKIP = new Set(["node_modules", "dist", "coverage", ".turbo"]);
const CHECKED = /\.(?:css|scss|ts|tsx|mts|cts|js|jsx|mjs|cjs|html|vue|svelte)$/;
const TEST = /\.(?:test|spec)\.[cm]?[jt]sx?$/;

function* files(dir) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!SKIP.has(entry.name)) yield* files(path);
    } else if (CHECKED.test(entry.name) && !TEST.test(entry.name) && !entry.name.endsWith(".d.ts") && !isThemeFile(path)) {
      yield path;
    }
  }
}

const folders = process.argv.slice(2);
let count = 0;
for (const folder of folders.length ? folders : ["src"]) {
  if (!existsSync(folder) || !statSync(folder).isDirectory()) {
    console.error(`framework-ui-check-colors: folder not found: ${folder}`);
    process.exit(2);
  }
  for (const file of files(folder)) {
    for (const hit of findRawColors(readFileSync(file, "utf8"), file)) {
      count++;
      console.error(`${relative(process.cwd(), file)}:${hit.line}: a raw colour: ${hit.text}`);
    }
  }
}
if (count > 0) {
  console.error(
    `framework-ui-check-colors: ${count} raw colour(s). Use a theme variable (var(--primary), bg-primary), ` +
      `set the colour in theme.css, or add the comment ${RAW_COLOR_ALLOWED} with the reason.`,
  );
  process.exit(1);
}
console.log("framework-ui-check-colors: no raw colours.");
