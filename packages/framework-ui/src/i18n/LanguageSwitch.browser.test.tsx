import { I18nextProvider } from "react-i18next";
import { describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { englishI18n } from "../testing/browser";
import { LanguageSwitch } from "./LanguageSwitch";

async function renderSwitch(props: Partial<Parameters<typeof LanguageSwitch>[0]> & Pick<Parameters<typeof LanguageSwitch>[0], "changeLanguage">) {
  const app = await englishI18n();
  return render(
    <I18nextProvider i18n={app.i18n}>
      <LanguageSwitch {...props} />
    </I18nextProvider>,
  );
}

describe("LanguageSwitch in a browser", () => {
  it("is a labelled select of every language, named in its own words", async () => {
    const screen = await renderSwitch({ changeLanguage: async () => true });
    const select = screen.getByRole("combobox", { name: "Language / Jezik" });
    await expect.element(select).toHaveValue("en-US");
    expect(screen.getByRole("option").elements().map((o) => o.textContent)).toEqual(["English", "Slovenščina"]);
  });

  it("is busy while the catalog loads, then reports the change", async () => {
    let finish: (ok: boolean) => void = () => {};
    const changeLanguage = vi.fn(() => new Promise<boolean>((resolve) => (finish = resolve)));
    const onChanged = vi.fn();
    const screen = await renderSwitch({ changeLanguage, onChanged });
    const select = screen.getByRole("combobox");

    await userEvent.selectOptions(select, "sl-SI");
    expect(changeLanguage).toHaveBeenCalledWith("sl-SI");
    await expect.element(select).toBeDisabled();
    await expect.element(select).toHaveAttribute("aria-busy", "true");

    finish(true);
    await expect.element(select).toBeEnabled();
    expect(onChanged).toHaveBeenCalledWith("sl-SI");
  });

  it("is reached with Tab and switches with the arrow keys", async () => {
    const changeLanguage = vi.fn(async () => true);
    const screen = await renderSwitch({ changeLanguage });
    const select = screen.getByRole("combobox");

    await userEvent.tab();
    await expect.element(select).toHaveFocus();
    await userEvent.keyboard("{ArrowDown}");
    await vi.waitFor(() => expect(changeLanguage).toHaveBeenCalledWith("sl-SI"));
  });

  it("reports a catalog that fails to load and stays usable", async () => {
    const failure = new Error("offline");
    const onChanged = vi.fn();
    const onError = vi.fn();
    const screen = await renderSwitch({ changeLanguage: () => Promise.reject(failure), onChanged, onError });
    const select = screen.getByRole("combobox");

    await userEvent.selectOptions(select, "sl-SI");
    await expect.element(select).toBeEnabled();
    expect(onError).toHaveBeenCalledWith(failure);
    expect(onChanged).not.toHaveBeenCalled();
  });
});
