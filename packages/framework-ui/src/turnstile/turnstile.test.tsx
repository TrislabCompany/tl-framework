import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { TURNSTILE_HEADER, TURNSTILE_TEST_SITE_KEY, Turnstile, loadTurnstile } from "./Turnstile";

describe("Turnstile", () => {
  it("renders a container for the widget and no alert before it loads", () => {
    const html = renderToStaticMarkup(<Turnstile siteKey={TURNSTILE_TEST_SITE_KEY} onToken={() => {}} className="bot-check" />);
    expect(html).toBe('<div class="bot-check"><div></div></div>');
  });

  it("names the header the API reads", () => {
    expect(TURNSTILE_HEADER).toBe("X-Turnstile-Token");
  });

  it("refuses to load outside a browser", async () => {
    await expect(loadTurnstile()).rejects.toThrow("browser");
  });
});
