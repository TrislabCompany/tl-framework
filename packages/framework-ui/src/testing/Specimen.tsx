// Test-only: not built into dist (tsconfig.build.json). A page of the package's components and the
// theme's colours, rendered by the browser tests in light and dark for axe and the screenshots.
// Every colour is a theme variable, so the page shows exactly what theme.css sets.
import type { CSSProperties, ReactNode } from "react";
import { LanguageSwitch } from "../i18n/LanguageSwitch";

const card: CSSProperties = {
  background: "var(--card)",
  color: "var(--card-foreground)",
  border: "1px solid var(--border)",
  borderRadius: "var(--radius)",
  padding: "12px 16px",
  margin: "12px 0",
};

function Button({ background, color, children }: { background: string; color: string; children: ReactNode }) {
  return (
    <button
      type="button"
      style={{ background: `var(${background})`, color: `var(${color})`, border: 0, borderRadius: "var(--radius)", padding: "8px 14px", marginRight: 8, font: "inherit" }}
    >
      {children}
    </button>
  );
}

export function Specimen({ changeLanguage }: { changeLanguage: (language: string) => Promise<boolean> }) {
  return (
    <main style={{ padding: 16 }}>
      {/* Form controls take Inter too: the system font differs between machines and would move the screenshots. */}
      <style>{".language select { font: inherit; margin-left: 8px; }"}</style>
      <h1 style={{ fontSize: 22, margin: "0 0 4px" }}>Trislab</h1>
      <p style={{ margin: 0, color: "var(--muted-foreground)" }}>
        Muted text, and a <a href="#top" style={{ color: "var(--primary-text)" }}>link</a> on the background.
      </p>
      <section style={card} aria-label="Actions">
        <Button background="--primary" color="--primary-foreground">Save</Button>
        <Button background="--secondary" color="--secondary-foreground">Cancel</Button>
        <Button background="--destructive" color="--destructive-foreground">Delete</Button>
      </section>
      <section style={card} aria-label="Settings">
        <LanguageSwitch changeLanguage={changeLanguage} className="language" />
        <p role="alert" style={{ margin: "8px 0 0", padding: "6px 10px", borderRadius: "var(--radius)", background: "var(--warning)", color: "var(--warning-foreground)" }}>
          The security check could not load.
        </p>
      </section>
    </main>
  );
}
