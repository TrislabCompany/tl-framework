/** The user's choice of colours: always light, always dark, or as the operating system says. */
export type ColorScheme = "light" | "dark" | "system";

export type ApplyColorSchemeOptions = {
  /** The element that carries the class `dark`; `document.documentElement` by default. */
  root?: Element;
  /** The query of the operating system's setting; `window.matchMedia` by default. */
  matchMedia?: (query: string) => MediaQueryList;
};

const DARK_QUERY = "(prefers-color-scheme: dark)";

/**
 * Puts the class `dark` on `<html>` when the scheme is dark, which switches theme.css to its
 * dark colours. With `system` it follows the operating system, also when it changes later.
 * Returns a function that stops following it.
 */
export function applyColorScheme(scheme: ColorScheme, options: ApplyColorSchemeOptions = {}): () => void {
  const root = options.root ?? document.documentElement;
  if (scheme !== "system") {
    root.classList.toggle("dark", scheme === "dark");
    return () => {};
  }

  const matchMedia = options.matchMedia ?? window.matchMedia.bind(window);
  const query = matchMedia(DARK_QUERY);
  const follow = () => root.classList.toggle("dark", query.matches);
  follow();
  query.addEventListener("change", follow);
  return () => query.removeEventListener("change", follow);
}
