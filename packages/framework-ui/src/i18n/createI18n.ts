import i18next, { type i18n as I18n } from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import {
  DEFAULT_LANGUAGE,
  SUPPORTED_LANGUAGES,
  type Language,
  fallbackChain,
  missingKeyFallback,
  toCanonical,
} from "./languages";

export type Catalog = { [key: string]: string | Catalog };

type CatalogModule = { default: Catalog };
/** Every supported language but en-US, which ships with the app. */
type TranslatedLanguage = Exclude<Language, "en-US">;
type LanguageModule = { default: Record<string, Catalog> };

export interface FrameworkI18nOptions<Lazy extends string> {
  /**
   * The app's key for the choice it keeps in localStorage, such as `my-product.web`. Two apps on
   * one origin need different keys, or one would switch the other's language.
   */
  storageKey: string;
  /** en-US catalogs the app shell needs on its first paint, by namespace. */
  eager: Record<string, Catalog>;
  /** en-US catalogs only some routes need, loaded with them through `loadNamespaces`. */
  lazy?: Record<Lazy, () => Promise<CatalogModule>>;
  /**
   * Every other language as one lazy chunk of all its namespaces, keyed by namespace.
   * A Vite app writes it with `import.meta.glob("./sl-SI/*.json", { eager: true })`.
   */
  languages?: Partial<Record<TranslatedLanguage, () => Promise<LanguageModule>>>;
  /** The namespace `t()` uses without a prefix. Default `common`. */
  defaultNamespace?: string;
  /** Called for a missing key in development, e.g. to warn. */
  onMissingKey?: (key: string) => void;
}

export interface FrameworkI18n<Lazy extends string> {
  i18n: I18n;
  /** Starts i18next. Call once, after the runtime config is read, with the deployment's default language. */
  init: (deploymentDefault?: string) => Promise<void>;
  /**
   * Switches the language after its catalog has loaded, so the UI never re-renders in a language
   * whose text isn't there. Resolves false when a later switch landed first; rejects when the
   * catalog fails to load, and then the language doesn't change.
   */
  changeLanguage: (language: string) => Promise<boolean>;
  /** Loads route namespaces into en-US, in parallel with the route's chunk. */
  loadNamespaces: (namespaces: readonly Lazy[]) => Promise<void>;
  namespaces: string[];
}

/**
 * One i18next instance set up the way every framework app runs it: en-US as the terminal
 * fallback, other languages as one lazy chunk each, the choice kept per app in localStorage,
 * `?culture=` honoured, variants mapped onto the supported tags, and the document's `lang`
 * kept in step with the language on screen.
 */
export function createFrameworkI18n<Lazy extends string = never>(
  options: FrameworkI18nOptions<Lazy>,
): FrameworkI18n<Lazy> {
  const instance = i18next.createInstance();
  const lazy = (options.lazy ?? {}) as Record<Lazy, () => Promise<CatalogModule>>;
  const namespaces = [...Object.keys(options.eager), ...Object.keys(lazy)];
  const loadedLanguages = new Set<string>([DEFAULT_LANGUAGE]);
  const pendingNamespaces = new Map<Lazy, Promise<void>>();
  let latestSwitch = 0;

  async function loadLanguage(language: string) {
    const load = options.languages?.[language as TranslatedLanguage];
    if (!load || loadedLanguages.has(language)) return;
    const { default: bundle } = await load();
    for (const ns of namespaces) {
      const catalog = bundle[ns];
      if (catalog) instance.addResourceBundle(language, ns, catalog, true, true);
    }
    loadedLanguages.add(language);
  }

  async function changeLanguage(language: string): Promise<boolean> {
    const thisSwitch = ++latestSwitch;
    try {
      await loadLanguage(language);
    } catch (error) {
      if (thisSwitch !== latestSwitch) return false;
      throw error;
    }
    if (thisSwitch !== latestSwitch) return false;
    await instance.changeLanguage(language);
    return true;
  }

  function loadNamespaces(requested: readonly Lazy[]): Promise<void> {
    return Promise.all(
      requested.map((ns) => {
        let pending = pendingNamespaces.get(ns);
        if (!pending) {
          pending = lazy[ns]().then(
            ({ default: catalog }) => {
              instance.addResourceBundle(DEFAULT_LANGUAGE, ns, catalog, true, true);
            },
            (error: unknown) => {
              pendingNamespaces.delete(ns);
              throw error;
            },
          );
          pendingNamespaces.set(ns, pending);
        }
        return pending;
      }),
    ).then(() => undefined);
  }

  if (typeof document !== "undefined") {
    instance.on("languageChanged", (language) => {
      document.documentElement.lang = instance.resolvedLanguage ?? language;
    });
  }

  async function init(deploymentDefault: string = DEFAULT_LANGUAGE) {
    await instance
      .use(LanguageDetector)
      .use(initReactI18next)
      .init({
        resources: { [DEFAULT_LANGUAGE]: options.eager },
        fallbackLng: fallbackChain(deploymentDefault),
        supportedLngs: [...SUPPORTED_LANGUAGES],
        ns: namespaces,
        defaultNS: options.defaultNamespace ?? "common",
        interpolation: { escapeValue: false },
        parseMissingKeyHandler: (key: string, defaultValue?: string | null) => {
          options.onMissingKey?.(key);
          return missingKeyFallback(key, defaultValue);
        },
        detection: {
          order: ["querystring", "localStorage", "navigator"],
          caches: ["localStorage"],
          lookupLocalStorage: `${options.storageKey}.lng`,
          lookupQuerystring: "culture",
          convertDetectedLanguage: toCanonical,
        },
      });

    // Fetch a lazy detected language before the first render, so a sl-SI user never sees English
    // first. If its chunk fails, the app still starts, in English through the fallback chain.
    try {
      await changeLanguage(instance.language);
    } catch {
      // resolvedLanguage stays en-US; the detector keeps the user's choice for the next start.
    }
  }

  return { i18n: instance, init, changeLanguage, loadNamespaces, namespaces };
}
