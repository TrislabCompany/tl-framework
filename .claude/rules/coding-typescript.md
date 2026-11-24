---
paths:
  - "packages/**/*.ts"
  - "packages/**/*.tsx"
---

# TypeScript coding standards

Checked by: `tsc` with `strict` (`packages/*/tsconfig.json`, run by `npm test`), and the Vitest tests of each package. The apps that use the packages (and their ESLint and Playwright checks) are in `tl-product-saas`; this file is the code-level rules.

## Types

- No `any`. Use `unknown` and narrow it, or write the type. An `as` cast only at a boundary where the type is known (a parsed `config.json`), with a comment.
- DTO types are hand-written in `src/api/<feature>.ts`, with the API's field names. One type per response; don't reuse a response type as a form type.
- Prefer `type` for data shapes and unions, string-literal unions over `enum`.

## Components

- Function components, **named exports** (no default exports; routes load them through `lazyNamed`).
- One component per file, the file named after it in kebab-case (`user-list.tsx` exports `UserList`).
- Props typed with a `type <Name>Props`. No prop drilling past two levels: lift to a query, a context or the URL.
- Hooks follow the rules of hooks (lint-checked). Per-call data rides `mutate(arg)` (golden rule 9).

## Colours

- Colours come only from the theme's variables: `var(--primary)` in CSS, `bg-primary` or `text-muted-foreground` with Tailwind. Never a hex value, a colour function or Tailwind's palette (`bg-red-500`) in a component.
- Colours are set in a `theme.css`: the framework's in `packages/framework-ui/src/theme/`, a product's own in its app. A new variable goes in both `:root` and `.dark` when it differs, in `tailwind.css`, and in the contrast pairs of `theme.test.ts` when it carries text.
- `framework-ui-check-colors` enforces it; `raw-color-allowed: <why>` on the line is the only exception.

## Errors and logging

- The API client throws `ApiRequestError`; a page shows it in its error state (`error-band`, `route-error`) or a `sonner` toast. Never swallow a rejected promise.
- No `console.log` in committed code. A `console.warn` or `console.error` only where the user can't be told (a failed background refresh), with a short `<area>: <what>` message and no tokens or personal data.
- The browser never logs to the API. The trace id the user quotes comes from the ProblemDetails `traceId` (see `logging.md`), so show it in the error message.

## Naming

| Thing | Shape | Example |
|---|---|---|
| Component | PascalCase | `TenantSwitcher` |
| Hook | `use<Thing>` | `useCurrentUser` |
| API function | `<verb><Thing>` | `searchUsers`, `getUser` |
| Constant | `UPPER_SNAKE` for module-level config | `PAGE_SIZE` |
| File | kebab-case | `tenant-switcher.tsx` |

## Good and bad

```tsx
// good
export function UserBadge({ user }: UserBadgeProps) {
  return <span title={user.email}>{user.userName}</span>;
}
type UserBadgeProps = { user: UserDto };

// bad: default export, any, logging personal data
export default function Badge(props: any) { console.log(props.user.email); return <span>{props.user.userName}</span>; }
```
