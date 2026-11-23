import { renderToStaticMarkup } from "react-dom/server";
import { I18nextProvider } from "react-i18next";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createFrameworkI18n } from "./createI18n";
import { formatDate, formatMoney, formatNumber } from "./format";
import { LanguageSwitch } from "./LanguageSwitch";
import { fallbackChain, missingKeyFallback, toCanonical } from "./languages";

describe("languages", () => {
  it("maps variants onto the supported tags", () => {
    expect(toCanonical("sl")).toBe("sl-SI");
    expect(toCanonical("en-GB")).toBe("en-US");
    expect(toCanonical("sl-SI")).toBe("sl-SI");
    expect(toCanonical("de-DE")).toBe("de-DE");
  });

  it("always ends the fallback chain in en-US", () => {
    expect(fallbackChain("sl-SI")).toEqual(["sl-SI", "en-US"]);
    expect(fallbackChain("en-US")).toBe("en-US");
    expect(fallbackChain("de-DE")).toBe("en-US");
  });

  it("renders the caller's default, or the key's last segment", () => {
    expect(missingKeyFallback("status.invoiced", "Invoiced")).toBe("Invoiced");
    expect(missingKeyFallback("status.invoiced")).toBe("Invoiced");
  });
});

describe("format", () => {
  it("formats by language", () => {
    // Slovenian groups thousands from five digits on (CLDR), so 12345.5, not 1234.5.
    expect(formatNumber(12345.5, "sl-SI")).toBe("12.345,5");
    expect(formatNumber(12345.5, "en-US")).toBe("12,345.5");
    expect(formatMoney(12345.5, "EUR", "sl-SI")).toBe("12.345,50\u00a0€");
    // Explicit fields, not dateStyle "short": its year has two or four digits depending on the ICU version.
    expect(formatDate("2026-11-23T12:00:00Z", "sl-SI", { day: "numeric", month: "numeric", year: "numeric", timeZone: "UTC" })).toBe("23. 11. 2026");
  });
});

describe("createFrameworkI18n", () => {
  // The detector reads the browser's language; pin it so the machine running the tests doesn't decide.
  beforeEach(() => {
    vi.stubGlobal("navigator", { language: "en-US", languages: ["en-US"] });
  });

  const setup = () =>
    createFrameworkI18n<"billing">({
      storageKey: "test.app",
      eager: { common: { hello: "Hello", nested: { save: "Save" } } },
      lazy: { billing: async () => ({ default: { invoice: "Invoice" } }) },
      languages: {
        "sl-SI": async () => ({
          default: { common: { hello: "Pozdravljeni", nested: { save: "Shrani" } }, billing: { invoice: "Račun" } },
        }),
      },
    });

  it("loads a language before switching to it, and falls back to en-US", async () => {
    const app = setup();
    await app.init("en-US");
    expect(app.i18n.t("hello")).toBe("Hello");

    expect(await app.changeLanguage("sl-SI")).toBe(true);
    expect(app.i18n.t("nested.save")).toBe("Shrani");
    expect(app.i18n.t("missing.key", { defaultValue: "Fallback" })).toBe("Fallback");
  });

  it("loads route namespaces on demand", async () => {
    const app = setup();
    await app.init("en-US");
    await app.loadNamespaces(["billing"]);
    expect(app.i18n.t("billing:invoice")).toBe("Invoice");
  });

  it("keeps the language when its catalog fails to load", async () => {
    const app = createFrameworkI18n({
      storageKey: "test.failing",
      eager: { common: { hello: "Hello" } },
      languages: { "sl-SI": () => Promise.reject(new Error("offline")) },
    });
    await app.init("en-US");
    await expect(app.changeLanguage("sl-SI")).rejects.toThrow("offline");
    expect(app.i18n.language).toBe("en-US");
  });
});

describe("LanguageSwitch", () => {
  beforeEach(() => {
    vi.stubGlobal("navigator", { language: "en-US", languages: ["en-US"] });
  });

  it("offers every language in its own words", async () => {
    const app = createFrameworkI18n({ storageKey: "test.switch", eager: { common: {} } });
    await app.init("en-US");
    const html = renderToStaticMarkup(
      <I18nextProvider i18n={app.i18n}>
        <LanguageSwitch changeLanguage={app.changeLanguage} />
      </I18nextProvider>,
    );
    expect(html).toContain("English");
    expect(html).toContain("Slovenščina");
    expect(html).toContain('lang="sl-SI"');
  });
});
