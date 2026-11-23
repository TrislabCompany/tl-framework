import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { LANGUAGE_NAMES, SUPPORTED_LANGUAGES, toCanonical } from "./languages";

export interface LanguageSwitchProps {
  /** The `changeLanguage` of `createFrameworkI18n`, which loads the catalog before switching. */
  changeLanguage: (language: string) => Promise<boolean>;
  /** The visible label. Default "Language / Jezik", readable whichever language is on screen. */
  label?: string;
  /** Called after the switch, e.g. to save the user's `locale` through the API. */
  onChanged?: (language: string) => void;
  /** Called when the catalog failed to load; the language stays as it was. */
  onError?: (error: unknown) => void;
  className?: string;
}

/**
 * A native select of the supported languages, each named in its own words. Unstyled: pass a
 * `className`. A native select is keyboard and screen-reader accessible as it is.
 */
export function LanguageSwitch({ changeLanguage, label = "Language / Jezik", onChanged, onError, className }: LanguageSwitchProps) {
  const { i18n } = useTranslation();
  const id = useId();
  const [busy, setBusy] = useState(false);
  const current = toCanonical(i18n.resolvedLanguage ?? i18n.language);

  async function onChange(language: string) {
    setBusy(true);
    try {
      if (await changeLanguage(language)) onChanged?.(language);
    } catch (error) {
      onError?.(error);
    } finally {
      setBusy(false);
    }
  }

  return (
    <span className={className}>
      <label htmlFor={id}>{label}</label>
      <select id={id} value={current} disabled={busy} aria-busy={busy} onChange={(event) => void onChange(event.target.value)}>
        {SUPPORTED_LANGUAGES.map((language) => (
          <option key={language} value={language} lang={language}>
            {LANGUAGE_NAMES[language]}
          </option>
        ))}
      </select>
    </span>
  );
}
