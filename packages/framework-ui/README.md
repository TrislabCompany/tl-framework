# @trislabcompany/framework-ui

The shared React UI of `tl-framework`, published to GitHub Packages at the framework's version ([docs/packages.md](../../docs/packages.md)). The starters `tl-product-webapp` and `tl-product-saas` build their front ends on it.

It holds the Trislab theme, i18n and the bot check of public forms. English (`en-US`) and Slovenian (`sl-SI`) ship with the framework, and every text is in both.

| Export | What it does |
| --- | --- |
| `createFrameworkI18n(options)` | One i18next instance, set up as every framework app runs it:<br>- en-US catalogs eager (the app shell) or lazy (per route, `loadNamespaces`);<br>- every other language as one lazy chunk, loaded before the switch (`changeLanguage`);<br>- the user's choice kept per app in localStorage, and `?culture=` honoured;<br>- variants mapped onto the supported tags;<br>- the document's `lang` kept in step |
| `LanguageSwitch` | A native, accessible select of the languages, each named in its own words. Unstyled; `onChanged` saves the user's `locale` through the API |
| `formatDate`, `formatDateTime`, `formatNumber`, `formatMoney` | Format a value in the language on screen. The API sends values, never formatted text |
| `SUPPORTED_LANGUAGES`, `toCanonical`, `fallbackChain`, `missingKeyFallback` | The language rules the API follows too (`SupportedCultures.Tags`) |
| `theme.css`, `fonts.css`, `tailwind.css` | The Trislab theme as CSS variables, Inter, and the variables as Tailwind colours ([Theme](#theme)) |
| `applyColorScheme(scheme)` | Light, dark, or as the operating system says: puts the class `dark` on `<html>` |
| `framework-ui-check-colors` | A command that fails on a colour written into code instead of a theme variable ([Theme](#theme)) |
| `Turnstile`, `TURNSTILE_HEADER` | Cloudflare Turnstile, the bot check of a public form; the token goes in `X-Turnstile-Token` to an endpoint with `RequireTurnstile()` ([bot-protection.md](../../docs/bot-protection.md)) |

```tsx
import { createFrameworkI18n, LanguageSwitch } from "@trislabcompany/framework-ui";
import common from "./locales/en-US/common.json";

export const i18n = createFrameworkI18n({
  storageKey: "my-product.web",
  eager: { common },
  lazy: { billing: () => import("./locales/en-US/billing.json") },
  languages: { "sl-SI": () => import("./locales/sl-SI") },
});

await i18n.init(config.PUBLIC_DEFAULT_LANGUAGE); // the deployment's default, from /config.json

<LanguageSwitch changeLanguage={i18n.changeLanguage} onChanged={(locale) => saveLocale(locale)} />;
```

## Theme

Every framework app looks like Trislab by default: dark slate, the orange accent `#f97316`, Inter and a radius of `0.75rem`, the colours of the Trislab website. They are CSS variables with the shadcn names (`--background`, `--foreground`, `--primary`, `--primary-foreground`, `--muted`, `--border`, `--ring`, `--radius`, `--font-sans`, …), light by default and dark under the class `dark`.

```css
/* src/styles.css of the app */
@import "@trislabcompany/framework-ui/theme.css";
@import "@trislabcompany/framework-ui/fonts.css"; /* Inter; leave it out for another font */
@import "./theme.css"; /* the product's own look, the only file with colours */
```

With Tailwind CSS v4, add `@import "tailwindcss";` first and `@import "@trislabcompany/framework-ui/tailwind.css";` after the theme, so `bg-primary`, `text-muted-foreground` and `rounded-lg` follow it.

**A product changes its look in one file**, its own `theme.css`, which overrides only the variables it needs:

```css
:root { --primary: #2563eb; --primary-foreground: #ffffff; --primary-text: #1d4ed8; --ring: #1d4ed8; }
.dark { --primary: #3b82f6; --primary-foreground: #0f172a; --primary-text: #60a5fa; --ring: #60a5fa; }
```

Keep the contrast: every `--*-foreground` needs 4.5:1 on its colour, and `--primary-text` and `--muted-foreground` on `--background`, `--card` and `--muted` (WCAG 2 AA). The framework's own values are checked by `theme.test.ts`.

**No raw colours outside a `theme.css`.** `framework-ui-check-colors src` fails on a hex value, a colour function (`rgb(`, `oklch(`, …) or Tailwind's own palette (`bg-red-500`, `text-white`) in CSS or in a script. A `theme.css` is not checked. A colour that must stay fixed, such as a logo's, gets the comment `raw-color-allowed: <why>` on its line. Run it with the app's other checks.

Peer dependencies: `react` 19, `i18next` 26, `react-i18next` 17, `i18next-browser-languagedetector` 8. Dependency: `@fontsource-variable/inter` 5, for `fonts.css`.

## Working on it

`npm ci`, then `npm test`: the type check, Vitest, the build, and `framework-ui-check-colors` on the package's own `src`. In a clone, `node cli/check-colors.mjs <folder>` needs `npm run build` first. `ci / ci` runs `npm test` for every package in `packages/`. A release tag publishes it (`.github/workflows/packages.yml`).

Vitest has two projects ([vitest.config.ts](vitest.config.ts)):

| Project | Files | What it checks |
| --- | --- | --- |
| `unit` | `*.test.ts(x)` | In Node: the logic, the theme's contrast as numbers, the colour check |
| `browser` | `*.browser.test.tsx` | In Chromium, through Playwright, with the theme's CSS and Inter loaded: each component's behaviour (keyboard, busy and error states), the WCAG 2.2 AA rules of axe, contrast included, and screenshots of every component in light and dark |

- **First run:** `npx playwright install chromium`.
- **The screenshots** are kept for Linux, the platform of CI, in `src/**/__screenshots__`; on Windows and macOS those tests are skipped. After a change to how a component or the theme looks, `npm run test:screenshots` makes them again in Playwright's Linux image (Docker needed). Look at each image before committing it.
- **A new component** gets a `*.browser.test.tsx` and a place in the page of all components, `src/testing/Specimen.tsx`, so axe and the screenshots cover it.
- **Whole pages** are tested in the starters, where they live. Once the upgrade rehearsal exists, their Playwright tests are to run in it against each framework release.
