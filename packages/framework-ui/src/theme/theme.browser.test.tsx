import { I18nextProvider } from "react-i18next";
import { afterEach, describe, expect, it } from "vitest";
import { server } from "vitest/browser";
import { render } from "vitest-browser-react";
import { Specimen } from "../testing/Specimen";
import { SCHEMES, accessibilityViolations, englishI18n, useScheme } from "../testing/browser";

// The screenshots are kept for Linux only, the platform of CI. Make or update them with
// `npm run test:screenshots` (README.md, "Working on it"), which runs in Playwright's Linux image.
const screenshots = server.platform === "linux";

describe("the Trislab theme in a browser", () => {
  afterEach(() => document.documentElement.classList.remove("dark"));

  for (const scheme of SCHEMES) {
    it(`${scheme}: the components have no WCAG 2.2 AA violations, contrast included`, async () => {
      await useScheme(scheme);
      const app = await englishI18n();
      const screen = await render(
        <I18nextProvider i18n={app.i18n}>
          <Specimen changeLanguage={app.changeLanguage} />
        </I18nextProvider>,
      );
      await expect.element(screen.getByText("report.pdf")).toBeVisible(); // the attachments have loaded
      expect(await accessibilityViolations(screen.container)).toEqual([]);
    });

    it.skipIf(!screenshots)(`${scheme}: looks as in the screenshot`, async () => {
      await useScheme(scheme);
      const app = await englishI18n();
      const screen = await render(
        <I18nextProvider i18n={app.i18n}>
          <Specimen changeLanguage={app.changeLanguage} />
        </I18nextProvider>,
      );
      await expect.element(screen.getByText("report.pdf")).toBeVisible();
      await expect.element(screen.getByRole("main")).toMatchScreenshot(`specimen-${scheme}`);
    });
  }

  it("sets the page's colours, font and radius from the variables", async () => {
    await useScheme("dark");
    const body = getComputedStyle(document.body);
    expect(body.backgroundColor).toBe("rgb(15, 23, 42)"); // #0f172a, the dark background
    expect(body.fontFamily).toMatch(/^"Inter Variable"/);
    expect(document.fonts.check('16px "Inter Variable"')).toBe(true);
    expect(getComputedStyle(document.documentElement).getPropertyValue("--radius").trim()).toBe("0.75rem");
  });
});
