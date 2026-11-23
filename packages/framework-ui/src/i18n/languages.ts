/**
 * The languages the framework ships, as the specific tags the API's `SupportedCultures.Tags`
 * accepts, `User.Locale` stores and the `locale` claim carries. en-US is the terminal fallback:
 * it is the only catalog every other language falls back to.
 */
export const SUPPORTED_LANGUAGES = ["en-US", "sl-SI"] as const;

export type Language = (typeof SUPPORTED_LANGUAGES)[number];

export const DEFAULT_LANGUAGE: Language = "en-US";

/** Each language in its own words, for the language switch. */
export const LANGUAGE_NAMES: Record<Language, string> = {
  "en-US": "English",
  "sl-SI": "Slovenščina",
};

export function isSupportedLanguage(value: string): value is Language {
  return (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
}

/**
 * Maps any variant onto a supported tag by its language part: `sl` and `sl-Latn` become
 * `sl-SI`, `en` and `en-GB` become `en-US`. i18next's `nonExplicitSupportedLngs` does not
 * rewrite them, so the detector's `convertDetectedLanguage` uses this. A language the
 * framework doesn't ship is returned as it is, and i18next falls back from it.
 */
export function toCanonical(language: string): string {
  if (isSupportedLanguage(language)) return language;
  const base = language.split("-")[0]?.toLowerCase() ?? "";
  return SUPPORTED_LANGUAGES.find((tag) => tag.split("-")[0] === base) ?? language;
}

/**
 * The language chain a missing key walks. en-US is the only catalog guaranteed complete, so
 * the chain always ends there. A deployment that defaults to sl-SI keeps sl-SI first, so a
 * browser language the app doesn't offer lands on the deployment's language.
 */
export function fallbackChain(deploymentDefault: string): string | string[] {
  if (deploymentDefault !== DEFAULT_LANGUAGE && isSupportedLanguage(deploymentDefault)) {
    return [deploymentDefault, DEFAULT_LANGUAGE];
  }
  return DEFAULT_LANGUAGE;
}

/**
 * What renders when a key is in no catalog. A caller that passes an English `defaultValue`
 * gets it back. A caller that builds the key from a server value (`status.${x}`) gets the
 * key's last segment, capitalized: the readable name the UI showed before it was localized.
 */
export function missingKeyFallback(key: string, defaultValue?: string | null): string {
  if (typeof defaultValue === "string") return defaultValue;
  const segment = key.split(/[.:]/).pop() ?? key;
  return segment.charAt(0).toUpperCase() + segment.slice(1);
}
