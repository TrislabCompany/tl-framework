/**
 * Formatting of dates, numbers and amounts by language. The API sends values, never formatted
 * text (its formatting culture is invariant), so the UI formats them here, in the language on
 * screen. The formatters are cached per language and options.
 */
const cache = new Map<string, Intl.DateTimeFormat | Intl.NumberFormat>();

function cached<T extends Intl.DateTimeFormat | Intl.NumberFormat>(key: string, create: () => T): T {
  let formatter = cache.get(key) as T | undefined;
  if (!formatter) {
    formatter = create();
    cache.set(key, formatter);
  }
  return formatter;
}

type DateInput = Date | string | number;

const toDate = (value: DateInput) => (value instanceof Date ? value : new Date(value));

/** A date such as 23. 11. 2026 (sl-SI) or 11/23/2026 (en-US). */
export function formatDate(value: DateInput, language: string, options: Intl.DateTimeFormatOptions = { dateStyle: "medium" }): string {
  const formatter = cached(`d|${language}|${JSON.stringify(options)}`, () => new Intl.DateTimeFormat(language, options));
  return formatter.format(toDate(value));
}

/** A date and time, in the user's time zone unless `options.timeZone` says otherwise. */
export function formatDateTime(value: DateInput, language: string, options: Intl.DateTimeFormatOptions = { dateStyle: "medium", timeStyle: "short" }): string {
  return formatDate(value, language, options);
}

/** A number such as 1.234,5 (sl-SI) or 1,234.5 (en-US). */
export function formatNumber(value: number, language: string, options: Intl.NumberFormatOptions = {}): string {
  const formatter = cached(`n|${language}|${JSON.stringify(options)}`, () => new Intl.NumberFormat(language, options));
  return formatter.format(value);
}

/** An amount in a currency (ISO 4217, such as EUR), such as 1.234,50 € (sl-SI). */
export function formatMoney(amount: number, currency: string, language: string, options: Intl.NumberFormatOptions = {}): string {
  return formatNumber(amount, language, { style: "currency", currency, ...options });
}
