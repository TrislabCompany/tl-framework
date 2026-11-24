// Test-only helpers of the browser tests: not built into dist (tsconfig.build.json).
import axe from "axe-core";
import { createFrameworkI18n } from "../i18n/createI18n";
import { applyColorScheme } from "../theme/colorScheme";
import "../theme/theme.css";
import "../theme/fonts.css";

export const SCHEMES = ["light", "dark"] as const;

/** Puts the page in light or dark, as an app does, and waits for Inter. */
export async function useScheme(scheme: (typeof SCHEMES)[number]): Promise<void> {
  applyColorScheme(scheme);
  await document.fonts.ready;
}

/** An English i18n instance, as an app starts it. */
export async function englishI18n() {
  localStorage.removeItem("framework-ui.browser-test"); // no language saved by an earlier test
  const app = createFrameworkI18n({ storageKey: "framework-ui.browser-test", eager: { common: {} } });
  await app.init("en-US");
  return app;
}

/** The WCAG 2.2 A and AA violations axe finds in the element, as "rule: help (targets)" lines. */
export async function accessibilityViolations(element: Element): Promise<string[]> {
  const result = await axe.run(element, { runOnly: { type: "tag", values: ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"] } });
  return result.violations.map((v) => `${v.id}: ${v.help} (${v.nodes.map((n) => n.target.join(" ")).join(", ")})`);
}
