/** A colour written into code instead of a theme variable. */
export type RawColor = {
  line: number;
  text: string;
};

/** A line with this comment is allowed a raw colour, for example a logo's fixed colour. Say why after it. */
export const RAW_COLOR_ALLOWED = "raw-color-allowed";

const COLOR_FUNCTION = /\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(/;
const HEX = "#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})";
// In CSS a hex is a colour only in a declaration's value, after the colon; `url(#id)` is a reference.
const CSS_HEX = new RegExp(`:[^;{]*${HEX}\\b`);
// In a script a hex is a colour as a whole string ("#fff", fill="#fff", `#c62828`) or in a Tailwind arbitrary
// value (bg-[#fff]). A link to an anchor (href="#add", to="#top") or an id is not one.
const SCRIPT_HEX = new RegExp(
  `(?<!\\b(?:href|to|id|for|htmlFor|hash|src|action|target)=\\{?\\s*)["'\`]${HEX}["'\`]|\\[${HEX}\\]`,
);
// Tailwind's own palette (bg-red-500, text-white) bypasses the theme as much as a hex does.
const TAILWIND_PALETTE =
  /\b(?:bg|text|border(?:-[trblxy])?|ring|ring-offset|outline|fill|stroke|from|via|to|divide|placeholder|decoration|caret|accent|shadow)-(?:(?:slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-\d{2,3}|white|black)\b/;

/**
 * Finds the raw colours in one file. CSS may use only `var(--…)`; scripts may not use a hex value,
 * a colour function or Tailwind's palette. Comments are skipped. A `theme.css` is where colours are
 * set, so it is not checked (see {@link isThemeFile}).
 */
export function findRawColors(content: string, fileName: string): RawColor[] {
  const isCss = /\.(?:css|scss)$/.test(fileName);
  const patterns = isCss ? [COLOR_FUNCTION, CSS_HEX] : [COLOR_FUNCTION, SCRIPT_HEX, TAILWIND_PALETTE];
  const found: RawColor[] = [];
  let inComment = false;
  content.split(/\r?\n/).forEach((text, index) => {
    const [code, stillInComment] = withoutComments(text, inComment);
    inComment = stillInComment;
    if (text.includes(RAW_COLOR_ALLOWED) || /^\s*import\s/.test(code)) return;
    const value = isCss ? code.replace(/url\([^)]*\)/g, "") : code;
    if (patterns.some((p) => p.test(value))) found.push({ line: index + 1, text: text.trim() });
  });
  return found;
}

/** The files that set the colours, and so may hold raw ones: a `theme.css`. */
export function isThemeFile(fileName: string): boolean {
  return /(?:^|[\\/])theme\.css$/.test(fileName);
}

// The line without its /* … */ comments (which may span lines) and without a // comment, and whether a block
// comment is still open at its end. A // inside a string, as in "https://…", ends the line early, which can only
// hide a colour after it, never report a wrong one.
function withoutComments(line: string, inComment: boolean): [string, boolean] {
  let code = "";
  let rest = line;
  while (rest.length > 0) {
    if (inComment) {
      const end = rest.indexOf("*/");
      if (end < 0) return [code, true];
      rest = rest.slice(end + 2);
      inComment = false;
    } else {
      const start = rest.indexOf("/*");
      if (start < 0) {
        code += rest;
        break;
      }
      code += rest.slice(0, start);
      rest = rest.slice(start + 2);
      inComment = true;
    }
  }
  const lineComment = code.search(/(?<![:"'`])\/\//);
  return [lineComment < 0 ? code : code.slice(0, lineComment), inComment];
}
