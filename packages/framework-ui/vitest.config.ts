import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";

// Two projects: "unit" runs in Node (the logic, the theme's contrast maths, the colour check);
// "browser" renders the components in a real Chromium through Playwright, with the theme's CSS
// loaded, and checks behaviour, accessibility (axe) and screenshots, light and dark.
export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: "unit",
          include: ["src/**/*.test.{ts,tsx}"],
          exclude: ["src/**/*.browser.test.tsx"],
          environment: "node",
        },
      },
      {
        test: {
          name: "browser",
          include: ["src/**/*.browser.test.tsx"],
          browser: {
            enabled: true,
            provider: playwright(),
            headless: true,
            instances: [{ browser: "chromium" }],
            viewport: { width: 640, height: 480 },
            expect: {
              toMatchScreenshot: {
                comparatorName: "pixelmatch",
                // Anti-aliasing differs a little between machines; a real change moves far more.
                comparatorOptions: { allowedMismatchedPixelRatio: 0.01 },
              },
            },
          },
        },
      },
    ],
  },
});
