import { I18nextProvider } from "react-i18next";
import { afterEach, describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { SCHEMES, accessibilityViolations, englishI18n, useScheme } from "../testing/browser";
import { CURRENT_USER, fileAsset, memoryFilesClient } from "../testing/memoryFilesClient";
import { Attachments, type AttachmentsProps } from "./Attachments";
import { FilesRequestError, type FilesClient } from "./filesClient";

async function renderAttachments(client: FilesClient, props: Partial<AttachmentsProps> = {}, language?: string) {
  const app = await englishI18n();
  if (language) await app.i18n.changeLanguage(language);
  return render(
    <I18nextProvider i18n={app.i18n}>
      <Attachments client={client} ownerType="TestItem" ownerId="owner-1" category="Document" currentUserId={CURRENT_USER} {...props} />
    </I18nextProvider>,
  );
}

const twoFiles = () => memoryFilesClient([fileAsset("file-1", "report.pdf", 2_560), fileAsset("file-2", "minutes.pdf", 512, "user-2")]);

describe("Attachments in a browser", () => {
  afterEach(() => {
    document.documentElement.classList.remove("dark");
    vi.restoreAllMocks();
  });

  for (const scheme of SCHEMES) {
    it(`${scheme}: has no WCAG 2.2 AA violations`, async () => {
      await useScheme(scheme);
      const screen = await renderAttachments(twoFiles());
      await expect.element(screen.getByText("report.pdf")).toBeVisible();
      expect(await accessibilityViolations(screen.container)).toEqual([]);
    });
  }

  it("lists the owner's files with their sizes, and a delete button only on the user's own", async () => {
    const screen = await renderAttachments(twoFiles());

    await expect.element(screen.getByRole("region", { name: "Attachments" })).toBeVisible();
    await expect.element(screen.getByText("report.pdf")).toBeVisible();
    await expect.element(screen.getByText("2.5 kB")).toBeVisible();
    await expect.element(screen.getByRole("button", { name: "Delete report.pdf" })).toBeVisible();
    expect(screen.getByRole("button", { name: "Delete minutes.pdf" }).elements()).toHaveLength(0);
  });

  it("says so when there are no files", async () => {
    const screen = await renderAttachments(memoryFilesClient([]));
    await expect.element(screen.getByText("No files yet.")).toBeVisible();
  });

  it("uploads a chosen file and lists it", async () => {
    const client = twoFiles();
    const upload = vi.spyOn(client, "upload");
    const screen = await renderAttachments(client, { visibility: "Public" });

    await userEvent.upload(screen.getByLabelText("Add a file"), new File(["%PDF"], "offer.pdf", { type: "application/pdf" }));

    await expect.element(screen.getByText("offer.pdf")).toBeVisible();
    expect(upload).toHaveBeenCalledWith(expect.any(File), { ownerType: "TestItem", ownerId: "owner-1", category: "Document", visibility: "Public" });
  });

  it("deletes the user's file with the keyboard", async () => {
    const screen = await renderAttachments(twoFiles());
    const remove = screen.getByRole("button", { name: "Delete report.pdf" });
    await expect.element(remove).toBeVisible();

    (remove.element() as HTMLElement).focus();
    await userEvent.keyboard("{Enter}");

    await expect.element(screen.getByText("report.pdf")).not.toBeInTheDocument();
    await expect.element(screen.getByText("minutes.pdf")).toBeVisible();
  });

  it("opens a download URL in a new tab", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    const screen = await renderAttachments(twoFiles());

    await screen.getByRole("button", { name: "Download minutes.pdf" }).click();

    await vi.waitFor(() => expect(open).toHaveBeenCalledWith("https://storage.example.com/file-2", "_blank", "noopener"));
  });

  it("pages with Show more", async () => {
    const client = memoryFilesClient([fileAsset("f-1", "one.pdf", 1), fileAsset("f-2", "two.pdf", 1), fileAsset("f-3", "three.pdf", 1)]);
    const screen = await renderAttachments(client, { pageSize: 2 });
    await expect.element(screen.getByText("two.pdf")).toBeVisible();
    expect(screen.getByText("three.pdf").elements()).toHaveLength(0);

    await screen.getByRole("button", { name: "Show more" }).click();

    await expect.element(screen.getByText("three.pdf")).toBeVisible();
    expect(screen.getByRole("button", { name: "Show more" }).elements()).toHaveLength(0);
  });

  it("shows a failed list as an alert with its trace id, and reports it", async () => {
    const failure = new FilesRequestError(403, "Not allowed to list.", "00-abc-01");
    const client = { ...twoFiles(), listOwnerFiles: () => Promise.reject(failure) };
    const onError = vi.fn();
    const screen = await renderAttachments(client, { onError });

    await expect.element(screen.getByRole("alert")).toHaveTextContent("The files could not be listed. Reference: 00-abc-01");
    expect(onError).toHaveBeenCalledWith(failure);
  });

  it("speaks Slovenian when the app does", async () => {
    const screen = await renderAttachments(memoryFilesClient([]), {}, "sl-SI");

    await expect.element(screen.getByRole("region", { name: "Priloge" })).toBeVisible();
    await expect.element(screen.getByText("Datotek še ni.")).toBeVisible();
    await expect.element(screen.getByLabelText("Dodaj datoteko")).toBeVisible();
  });
});
