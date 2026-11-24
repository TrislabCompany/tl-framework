import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render } from "vitest-browser-react";
import { TURNSTILE_TEST_SITE_KEY, Turnstile } from "./Turnstile";

// Cloudflare's script is never loaded: window.turnstile stands in for it, so the tests run offline.
type Options = Record<string, unknown> & { callback: (token: string) => void; "expired-callback": () => void; "error-callback": () => void };

describe("Turnstile in a browser", () => {
  let options: Options | undefined;
  const remove = vi.fn();

  beforeEach(() => {
    options = undefined;
    remove.mockClear();
    window.turnstile = {
      render: (_container: HTMLElement, o: Record<string, unknown>) => ((options = o as Options), "widget-1"),
      remove,
    };
  });

  afterEach(() => {
    delete window.turnstile;
  });

  it("renders the widget with the site key, language and action", async () => {
    await render(<Turnstile siteKey={TURNSTILE_TEST_SITE_KEY} onToken={() => {}} language="sl-SI" action="contact" />);
    await vi.waitFor(() => expect(options).toBeDefined());
    expect(options).toMatchObject({ sitekey: TURNSTILE_TEST_SITE_KEY, language: "sl-SI", action: "contact" });
  });

  it("passes a token on, and null when it expires", async () => {
    const onToken = vi.fn();
    await render(<Turnstile siteKey={TURNSTILE_TEST_SITE_KEY} onToken={onToken} />);
    await vi.waitFor(() => expect(options).toBeDefined());

    options!.callback("token-1");
    options!["expired-callback"]();
    expect(onToken.mock.calls).toEqual([["token-1"], [null]]);
  });

  it("says so in an alert when the check fails, in the text it is given", async () => {
    const onToken = vi.fn();
    const screen = await render(<Turnstile siteKey={TURNSTILE_TEST_SITE_KEY} onToken={onToken} unavailableText="Preverjanje se ni naložilo." />);
    await vi.waitFor(() => expect(options).toBeDefined());

    options!["error-callback"]();
    await expect.element(screen.getByRole("alert")).toHaveTextContent("Preverjanje se ni naložilo.");
    expect(onToken).toHaveBeenCalledWith(null);
  });

  it("removes the widget when the form goes away", async () => {
    const screen = await render(<Turnstile siteKey={TURNSTILE_TEST_SITE_KEY} onToken={() => {}} />);
    await vi.waitFor(() => expect(options).toBeDefined());

    await screen.unmount();
    expect(remove).toHaveBeenCalledWith("widget-1");
  });
});
