# @trislabcompany/framework-ui

The shared React UI of `tl-framework`, published to GitHub Packages at the framework's version ([docs/packages.md](../../docs/packages.md)). The starters `tl-product-webapp` and `tl-product-saas` build their front ends on it.

It starts with i18n and the bot check of public forms. English (`en-US`) and Slovenian (`sl-SI`) ship with the framework, and every text is in both.

| Export | What it does |
| --- | --- |
| `createFrameworkI18n(options)` | One i18next instance, set up as every framework app runs it:<br>- en-US catalogs eager (the app shell) or lazy (per route, `loadNamespaces`);<br>- every other language as one lazy chunk, loaded before the switch (`changeLanguage`);<br>- the user's choice kept per app in localStorage, and `?culture=` honoured;<br>- variants mapped onto the supported tags;<br>- the document's `lang` kept in step |
| `LanguageSwitch` | A native, accessible select of the languages, each named in its own words. Unstyled; `onChanged` saves the user's `locale` through the API |
| `formatDate`, `formatDateTime`, `formatNumber`, `formatMoney` | Format a value in the language on screen. The API sends values, never formatted text |
| `SUPPORTED_LANGUAGES`, `toCanonical`, `fallbackChain`, `missingKeyFallback` | The language rules the API follows too (`SupportedCultures.Tags`) |
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

Peer dependencies: `react` 19, `i18next` 26, `react-i18next` 17, `i18next-browser-languagedetector` 8.

## Working on it

`npm ci`, then `npm test` (type check and Vitest) and `npm run build`. `ci / ci` runs `npm test` for every package in `packages/`. A release tag publishes it (`.github/workflows/packages.yml`).
