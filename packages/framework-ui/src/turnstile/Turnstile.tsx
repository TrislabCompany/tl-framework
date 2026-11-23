import { useEffect, useRef, useState } from "react";

/** The header the API's `RequireTurnstile()` reads the token from. */
export const TURNSTILE_HEADER = "X-Turnstile-Token";

/** Cloudflare's test site key that always passes, for local runs and tests. */
export const TURNSTILE_TEST_SITE_KEY = "1x00000000000000000000AA";

const SCRIPT_URL = "https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit";

interface TurnstileApi {
  render: (container: HTMLElement, options: Record<string, unknown>) => string;
  remove: (widgetId: string) => void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

let scriptPromise: Promise<TurnstileApi> | undefined;

/** Loads Cloudflare's script once per page. A failed load is forgotten, so the next render retries. */
export function loadTurnstile(): Promise<TurnstileApi> {
  if (typeof window === "undefined") return Promise.reject(new Error("Turnstile needs a browser"));
  if (window.turnstile) return Promise.resolve(window.turnstile);
  scriptPromise ??= new Promise<TurnstileApi>((resolve, reject) => {
    const script = document.createElement("script");
    script.src = SCRIPT_URL;
    script.async = true;
    script.onload = () => (window.turnstile ? resolve(window.turnstile) : reject(new Error("Turnstile did not load")));
    script.onerror = () => reject(new Error("Turnstile did not load"));
    document.head.appendChild(script);
  }).catch((error: unknown) => {
    scriptPromise = undefined;
    throw error;
  });
  return scriptPromise;
}

export interface TurnstileProps {
  /** The widget's site key: the `PUBLIC_TURNSTILE_SITE_KEY` setting from `/config.json`. */
  siteKey: string;
  /** Called with a token to send in the `X-Turnstile-Token` header, and with null when it expires or fails. */
  onToken: (token: string | null) => void;
  /** The widget's language, such as `sl-SI`; default the browser's. */
  language?: string;
  /** Shown, with role="alert", when the check can't load (blocked script, offline). Pass it in the user's language. */
  unavailableText?: string;
  /** The form's action, shown in Cloudflare's analytics. */
  action?: string;
  className?: string;
}

/**
 * Cloudflare Turnstile, the bot check of a public form. Most people see nothing or a checkbox;
 * the widget is accessible by itself. When it can't load, the component says so instead of
 * leaving the form silently unsendable.
 */
export function Turnstile({ siteKey, onToken, language, unavailableText = "The security check could not load. Reload the page or try again later.", action, className }: TurnstileProps) {
  const container = useRef<HTMLDivElement>(null);
  const tokenHandler = useRef(onToken);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    tokenHandler.current = onToken;
  }, [onToken]);

  useEffect(() => {
    let widgetId: string | undefined;
    let cancelled = false;

    loadTurnstile().then(
      (turnstile) => {
        if (cancelled || !container.current) return;
        widgetId = turnstile.render(container.current, {
          sitekey: siteKey,
          action,
          language: language ?? "auto",
          callback: (token: string) => tokenHandler.current(token),
          "expired-callback": () => tokenHandler.current(null),
          "error-callback": () => {
            tokenHandler.current(null);
            setUnavailable(true);
          },
        });
      },
      () => {
        if (!cancelled) setUnavailable(true);
      },
    );

    return () => {
      cancelled = true;
      if (widgetId) window.turnstile?.remove(widgetId);
    };
  }, [siteKey, language, action]);

  return (
    <div className={className}>
      <div ref={container} />
      {unavailable && <p role="alert">{unavailableText}</p>}
    </div>
  );
}
