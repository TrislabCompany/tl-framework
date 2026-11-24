/// <reference types="node" />
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { applyColorScheme } from "./colorScheme";
import { findRawColors, isThemeFile } from "./rawColors";

// Vitest doesn't load CSS, so the files are read as text.
const theme = readFileSync(new URL("./theme.css", import.meta.url), "utf8");
const tailwind = readFileSync(new URL("./tailwind.css", import.meta.url), "utf8");

// The variables of one block (":root" or ".dark"), with var(--x) resolved against the light ones.
function block(selector: string): Map<string, string> {
  const start = theme.indexOf(`${selector} {`);
  const body = theme.slice(start, theme.indexOf("}", start));
  const vars = new Map<string, string>();
  for (const m of body.matchAll(/(--[\w-]+):\s*([^;]+);/g)) vars.set(m[1]!, m[2]!.trim());
  return vars;
}

const light = block(":root");
const dark = new Map([...light, ...block(".dark")]);

function resolve(vars: Map<string, string>, name: string): string {
  let value = vars.get(name);
  for (let i = 0; value?.startsWith("var("); i++) value = vars.get(value.slice(4, -1));
  if (!value || !/^#[0-9a-f]{6}$/i.test(value)) throw new Error(`${name} is not a hex colour: ${value}`);
  return value;
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255;
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r! + 0.7152 * g! + 0.0722 * b!;
}

function contrast(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi! + 0.05) / (lo! + 0.05);
}

const PAIRS: [string, string][] = [
  ["--foreground", "--background"],
  ["--card-foreground", "--card"],
  ["--popover-foreground", "--popover"],
  ["--primary-foreground", "--primary"],
  ["--primary-foreground", "--primary-hover"],
  ["--secondary-foreground", "--secondary"],
  ["--muted-foreground", "--muted"],
  ["--accent-foreground", "--accent"],
  ["--destructive-foreground", "--destructive"],
  ["--success-foreground", "--success"],
  ["--warning-foreground", "--warning"],
  ["--info-foreground", "--info"],
  ["--muted-foreground", "--background"],
  ["--muted-foreground", "--card"],
  ["--primary-text", "--background"],
  ["--primary-text", "--card"],
  ["--primary-text", "--muted"],
];

describe("theme.css", () => {
  for (const [name, vars] of [["light", light], ["dark", dark]] as const) {
    it.each(PAIRS)(`${name}: %s on %s is at least 4.5:1 (WCAG 2 AA)`, (text, background) => {
      expect(contrast(resolve(vars, text), resolve(vars, background))).toBeGreaterThanOrEqual(4.5);
    });
  }

  for (const [name, vars] of [["light", light], ["dark", dark]] as const) {
    it(`${name}: the focus ring is at least 3:1 on the background and cards (WCAG 2 AA, non-text)`, () => {
      expect(contrast(resolve(vars, "--ring"), resolve(vars, "--background"))).toBeGreaterThanOrEqual(3);
      expect(contrast(resolve(vars, "--ring"), resolve(vars, "--card"))).toBeGreaterThanOrEqual(3);
    });
  }

  it("has the Trislab accent, font and radius", () => {
    expect(resolve(light, "--primary")).toBe("#f97316");
    expect(resolve(dark, "--background")).toBe("#0f172a");
    expect(light.get("--font-sans")).toMatch(/^"Inter Variable"/);
    expect(light.get("--radius")).toBe("0.75rem");
  });

  it("gives every colour of tailwind.css a variable in theme.css", () => {
    const used = [...tailwind.matchAll(/var\((--[\w-]+)\)/g)].map((m) => m[1]);
    expect(used.filter((v) => !light.has(v!))).toEqual([]);
  });

  it("sets in dark only what it sets in light", () => {
    expect([...block(".dark").keys()].filter((v) => !light.has(v))).toEqual([]);
  });
});

describe("findRawColors", () => {
  it("finds a colour in CSS outside a variable", () => {
    const css = ".a {\n  color: #fff;\n  background: rgb(0 0 0);\n  border-color: var(--border);\n}";
    expect(findRawColors(css, "src/app.css").map((h) => h.line)).toEqual([2, 3]);
  });

  it("ignores comments, selectors and references in CSS", () => {
    const css = [
      "/* the orange",
      "   #f97316 and rgb(0 0 0) */",
      "#fab, .x,",
      'a[href="#add"] {',
      "  fill: url(#abc);",
      "  color: var(--primary); /* not #fff */",
      "}",
    ].join("\n");
    expect(findRawColors(css, "a.css")).toEqual([]);
  });

  it("finds a hex value, a colour function and Tailwind's palette in a script", () => {
    const tsx = [
      'const style = { color: "#c62828" };',
      '<div className="bg-red-500 text-white" />',
      '<span className="bg-[#123456]" />',
      'const c = "rgba(0, 0, 0, 0.5)";',
      'const red = "#c62828";',
      '<svg fill="#fff" />',
      '<path stroke={"#ababab"} />',
      "const colors = [`#123`, '#12345678'];",
    ].join("\n");
    expect(findRawColors(tsx, "a.tsx").map((h) => h.line)).toEqual([1, 2, 3, 4, 5, 6, 7, 8]);
  });

  it("allows theme classes, links to an anchor, imports and a line marked as allowed", () => {
    const tsx = [
      '<a href="#add" className="bg-primary text-primary-foreground border-border" />',
      '<Link to="#top" id="#abc" />',
      "// a comment may name #fff",
      'const url = "https://example.com/#fade";',
      'const name = "#12345";',
      'import "./theme.css";',
      'const logo = "#0a66c2"; // raw-color-allowed: the LinkedIn logo',
    ].join("\n");
    expect(findRawColors(tsx, "a.tsx")).toEqual([]);
  });

  it("leaves a theme.css alone", () => {
    expect(isThemeFile("src/theme.css")).toBe(true);
    expect(isThemeFile("src\\styles\\theme.css")).toBe(true);
    expect(isThemeFile("src/app.css")).toBe(false);
  });
});

describe("applyColorScheme", () => {
  function fakeRoot() {
    const classes = new Set<string>();
    const root = {
      classList: {
        toggle: (name: string, on: boolean) => (on ? classes.add(name) : classes.delete(name), on),
      },
    } as unknown as Element; // a stand-in for <html>: the tests run without a DOM
    return { root, classes };
  }

  function fakeMedia(matches: boolean) {
    const listeners = new Set<() => void>();
    const query = {
      matches,
      addEventListener: (_: string, l: () => void) => listeners.add(l),
      removeEventListener: (_: string, l: () => void) => listeners.delete(l),
    };
    const change = (m: boolean) => ((query.matches = m), listeners.forEach((l) => l()));
    return { matchMedia: () => query as unknown as MediaQueryList, change, listeners };
  }

  it("sets and clears the class dark", () => {
    const { root, classes } = fakeRoot();
    applyColorScheme("dark", { root });
    expect(classes.has("dark")).toBe(true);
    applyColorScheme("light", { root });
    expect(classes.has("dark")).toBe(false);
  });

  it("follows the operating system with system, until stopped", () => {
    const { root, classes } = fakeRoot();
    const media = fakeMedia(true);
    const stop = applyColorScheme("system", { root, matchMedia: media.matchMedia });
    expect(classes.has("dark")).toBe(true);
    media.change(false);
    expect(classes.has("dark")).toBe(false);
    stop();
    expect(media.listeners.size).toBe(0);
  });
});
