import { createInstance } from "i18next";
import { expect, test } from "@playwright/test";
import { fallbackChain, missingKeyFallback } from "../../src/lib/i18n-fallback";

// A sl-SI translation is allowed to lag behind en-US, so a key sl-SI lacks must render the English
// string. The trap is a deployment whose default is sl-SI: a fallback chain of just the deployment
// default falls back from sl-SI to sl-SI, misses again, and the missing-key handler renders a
// capitalized key segment instead. Runs against the real i18next with the app's own handler.
const SUPPORTED = ["en-US", "sl-SI"] as const;

const instance = createInstance({
  lng: "sl-SI",
  fallbackLng: fallbackChain("sl-SI", SUPPORTED),
  supportedLngs: [...SUPPORTED],
  ns: ["identity"],
  defaultNS: "identity",
  resources: {
    "en-US": { identity: { title: "Users", greeting: "Hello" } },
    "sl-SI": { identity: { title: "Uporabniki" } },
  },
  interpolation: { escapeValue: false },
  parseMissingKeyHandler: missingKeyFallback,
});

test.beforeAll(async () => {
  await instance.init();
});

test.describe("fallback chain", () => {
  test("a key sl-SI lacks renders in English on a sl-SI deployment", () => {
    expect(instance.t("greeting")).toBe("Hello");
  });

  test("a translated key still renders in sl-SI", () => {
    expect(instance.t("title")).toBe("Uporabniki");
  });

  test("the chain always ends in en-US, without repeating it", () => {
    expect(fallbackChain("sl-SI", SUPPORTED)).toEqual(["sl-SI", "en-US"]);
    expect(fallbackChain("en-US", SUPPORTED)).toBe("en-US");
    expect(fallbackChain("fr-FR", SUPPORTED)).toBe("en-US");
  });
});
