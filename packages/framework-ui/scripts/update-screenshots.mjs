// npm run test:screenshots: makes or updates the reference screenshots of the browser tests
// (src/**/__screenshots__), which are kept for Linux, the platform of CI. It runs the browser tests
// with --update in Playwright's Linux image of the installed Playwright version, so a screenshot
// made on Windows or macOS never becomes the reference. Needs Docker. Review the images before committing.
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { version } = JSON.parse(readFileSync(require.resolve("playwright/package.json"), "utf8"));
const image = `mcr.microsoft.com/playwright:v${version}-noble`;

const result = spawnSync(
  "docker",
  [
    "run", "--rm",
    "-v", `${process.cwd()}:/work`,
    "-v", "/work/node_modules", // Linux's own node_modules, never the host's
    "-w", "/work",
    // On a Linux host, the images belong to you, not root.
    ...(process.getuid ? ["-u", `${process.getuid()}:${process.getgid()}`, "-e", "HOME=/tmp"] : []),
    image,
    "sh", "-c", "npm ci --no-audit --no-fund && npx vitest run --project browser --update",
  ],
  { stdio: "inherit", env: { ...process.env, MSYS_NO_PATHCONV: "1" } },
);
process.exit(result.status ?? 1);
