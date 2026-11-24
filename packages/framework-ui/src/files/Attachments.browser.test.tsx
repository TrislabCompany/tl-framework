import { I18nextProvider } from "react-i18next";
import { afterEach, describe, expect, it, vi } from "vitest";
import { userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { SCHEMES, accessibilityViolations, englishI18n, useScheme } from "../testing/browser";
import { CURRENT_USER, fileAsset, memoryFilesClient } from "../testing/memoryFilesClient";
import { Attachments, type AttachmentsProps } from "./Attachments";
import { FilesRequestError, type FileAsset, type FilesClient } from "./filesClient";

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

  it("opens the download tab in the click, then points it at the URL, with the button busy meanwhile", async () => {
    const tab = { opener: window as Window | null, location: { href: "" }, close: vi.fn() };
    const open = vi.spyOn(window, "open").mockReturnValue(tab as unknown as Window); // a stand-in tab
    let answer: (url: string) => void = () => {};
    const client = { ...twoFiles(), downloadUrl: () => new Promise<string>((resolve) => (answer = resolve)) };
    const screen = await renderAttachments(client);
    const download = screen.getByRole("button", { name: "Download minutes.pdf" });

    await download.click();

    expect(open).toHaveBeenCalledWith("", "_blank");
    expect(tab.opener).toBeNull();
    await expect.element(download).toBeDisabled();

    answer("https://storage.example.com/file-2?sig=1");
    await expect.element(download).toBeEnabled();
    expect(tab.location.href).toBe("https://storage.example.com/file-2?sig=1");
    expect(tab.close).not.toHaveBeenCalled();
  });

  it("opens a public file's URL directly", async () => {
    const open = vi.spyOn(window, "open").mockReturnValue(null);
    const client = memoryFilesClient([{ ...fileAsset("file-9", "logo.png", 10), publicUrl: "https://cdn.example.com/logo.png" }]);
    const screen = await renderAttachments(client);

    await screen.getByRole("button", { name: "Download logo.png" }).click();

    expect(open).toHaveBeenCalledWith("https://cdn.example.com/logo.png", "_blank", "noopener");
  });

  it("closes the tab and says so when the download URL fails", async () => {
    const tab = { opener: window as Window | null, location: { href: "" }, close: vi.fn() };
    vi.spyOn(window, "open").mockReturnValue(tab as unknown as Window);
    const client = { ...twoFiles(), downloadUrl: () => Promise.reject(new FilesRequestError(404, "Not found.")) };
    const screen = await renderAttachments(client);

    await screen.getByRole("button", { name: "Download minutes.pdf" }).click();

    await expect.element(screen.getByRole("alert")).toHaveTextContent("The file could not be downloaded.");
    expect(tab.close).toHaveBeenCalled();
  });

  it("says so when the browser blocks the download tab", async () => {
    vi.spyOn(window, "open").mockReturnValue(null);
    const screen = await renderAttachments(twoFiles());

    await screen.getByRole("button", { name: "Download minutes.pdf" }).click();

    await expect.element(screen.getByRole("alert")).toHaveTextContent("The file could not be downloaded.");
  });

  it("keeps the newest owner's files when an older answer arrives late", async () => {
    const answers = new Map<string, (files: FileAsset[]) => void>();
    const client: FilesClient = {
      ...twoFiles(),
      listOwnerFiles: (_type, ownerId) =>
        new Promise((resolve) =>
          answers.set(ownerId, (items) => resolve({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })),
        ),
    };
    const app = await englishI18n();
    const view = (ownerId: string) => (
      <I18nextProvider i18n={app.i18n}>
        <Attachments client={client} ownerType="TestItem" ownerId={ownerId} category="Document" />
      </I18nextProvider>
    );
    const screen = await render(view("owner-a"));
    await screen.rerender(view("owner-b"));
    const region = screen.getByRole("region", { name: "Attachments" });

    answers.get("owner-b")!([fileAsset("b-1", "for-b.pdf", 1)]);
    await expect.element(screen.getByText("for-b.pdf")).toBeVisible();
    answers.get("owner-a")!([fileAsset("a-1", "for-a.pdf", 1)]);
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(screen.getByText("for-a.pdf").elements()).toHaveLength(0);
    await expect.element(screen.getByText("for-b.pdf")).toBeVisible();
    await expect.element(region).toHaveAttribute("aria-busy", "false");
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
