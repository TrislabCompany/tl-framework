import { useCallback, useEffect, useId, useRef, useState, type ChangeEvent } from "react";
import { useTranslation } from "react-i18next";
import { formatNumber } from "../i18n/format";
import { toCanonical } from "../i18n/languages";
import { FilesRequestError, type FileAsset, type FileVisibility, type FilesClient } from "./filesClient";

/** The texts of `Attachments`. Both languages ship with it; pass `texts` to change any of them. */
export type AttachmentsTexts = {
  title: string;
  empty: string;
  add: string;
  /** `{name}` is the file being uploaded. */
  uploading: string;
  download: string;
  delete: string;
  more: string;
  loadFailed: string;
  uploadFailed: string;
  downloadFailed: string;
  deleteFailed: string;
  /** Follows an error that has a trace id; `{traceId}` is the id the user quotes to support. */
  reference: string;
};

export const ATTACHMENTS_TEXTS: Record<"en-US" | "sl-SI", AttachmentsTexts> = {
  "en-US": {
    title: "Attachments",
    empty: "No files yet.",
    add: "Add a file",
    uploading: "Uploading {name}…",
    download: "Download",
    delete: "Delete",
    more: "Show more",
    loadFailed: "The files could not be listed.",
    uploadFailed: "The file could not be uploaded.",
    downloadFailed: "The file could not be downloaded.",
    deleteFailed: "The file could not be deleted.",
    reference: "Reference: {traceId}",
  },
  "sl-SI": {
    title: "Priloge",
    empty: "Datotek še ni.",
    add: "Dodaj datoteko",
    uploading: "Nalagam {name} …",
    download: "Prenesi",
    delete: "Izbriši",
    more: "Pokaži več",
    loadFailed: "Seznama datotek ni bilo mogoče prebrati.",
    uploadFailed: "Datoteke ni bilo mogoče naložiti.",
    downloadFailed: "Datoteke ni bilo mogoče prenesti.",
    deleteFailed: "Datoteke ni bilo mogoče izbrisati.",
    reference: "Sklic: {traceId}",
  },
};

export type AttachmentsProps = {
  /** From `createFilesClient`, with the app's authenticated fetch. */
  client: FilesClient;
  /** The owner type the owning module registered an `IFileAccessPolicy` for. */
  ownerType: string;
  /** The record whose files these are. */
  ownerId: string;
  /** The `Files:Categories` key new files are uploaded under, such as `Document`. */
  category: string;
  /** Of new files. Default `Private`. */
  visibility?: FileVisibility;
  /** The signed-in user: only a file's uploader sees its delete button. The API checks it again. */
  currentUserId?: string;
  /** Hide the upload control, e.g. for a user who may only read. Default true. */
  canUpload?: boolean;
  /** Files per page. Default 20. */
  pageSize?: number;
  /** `accept` of the file input, such as `.pdf,.docx`. The API checks the category's extensions too. */
  accept?: string;
  texts?: Partial<AttachmentsTexts>;
  /** Called with every failed request, e.g. to report it. The component shows the error itself. */
  onError?: (error: unknown) => void;
  className?: string;
};

/**
 * The files of one record, through the Files module: list, upload, download and delete, so a module needs
 * no link table of its own. Native controls only, so it is keyboard and screen-reader accessible as it is,
 * and unstyled but for theme variables: pass a `className`.
 */
export function Attachments({
  client,
  ownerType,
  ownerId,
  category,
  visibility = "Private",
  currentUserId,
  canUpload = true,
  pageSize = 20,
  accept,
  texts,
  onError,
  className,
}: AttachmentsProps) {
  const { i18n } = useTranslation();
  const language = toCanonical(i18n.resolvedLanguage ?? i18n.language ?? "en-US");
  const text = { ...(language === "sl-SI" ? ATTACHMENTS_TEXTS["sl-SI"] : ATTACHMENTS_TEXTS["en-US"]), ...texts };

  const titleId = useId();
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const [files, setFiles] = useState<FileAsset[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const errorHandler = useRef(onError);

  useEffect(() => {
    errorHandler.current = onError;
  }, [onError]);

  const fail = useCallback((message: string, cause: unknown) => {
    const traceId = cause instanceof FilesRequestError ? cause.traceId : undefined;
    setError(traceId ? `${message} ${text.reference.replace("{traceId}", traceId)}` : message);
    errorHandler.current?.(cause);
  }, [text.reference]);

  // Loads the first `pages` pages again, so an upload or a delete never leaves a stale or doubled row.
  const load = useCallback(async (pages: number) => {
    setLoading(true);
    try {
      const loaded: FileAsset[] = [];
      let totalCount = 0;
      for (let pageNumber = 1; pageNumber <= pages; pageNumber++) {
        const page = await client.listOwnerFiles(ownerType, ownerId, { pageNumber, pageSize });
        loaded.push(...page.items);
        totalCount = page.totalCount;
        if (pageNumber >= page.totalPages) break;
      }
      setFiles(loaded);
      setTotal(totalCount);
      setError(null);
    } catch (cause) {
      fail(text.loadFailed, cause);
    } finally {
      setLoading(false);
    }
  }, [client, ownerType, ownerId, pageSize, fail, text.loadFailed]);

  const [pages, setPages] = useState(1);
  useEffect(() => {
    void load(pages);
  }, [load, pages]);

  async function onFileChosen(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) return;
    setUploading(file.name);
    setError(null);
    try {
      await client.upload(file, { ownerType, ownerId, category, visibility });
      await load(pages);
    } catch (cause) {
      fail(text.uploadFailed, cause);
    } finally {
      setUploading(null);
      if (input.current) input.current.value = ""; // the same file can be chosen again
    }
  }

  async function onDownload(file: FileAsset) {
    setError(null);
    try {
      const url = file.publicUrl ?? (await client.downloadUrl(file.id));
      window.open(url, "_blank", "noopener");
    } catch (cause) {
      fail(text.downloadFailed, cause);
    }
  }

  async function onDelete(file: FileAsset) {
    setError(null);
    try {
      await client.remove(file.id);
      await load(pages);
    } catch (cause) {
      fail(text.deleteFailed, cause);
    }
  }

  const busy = loading || uploading !== null;

  return (
    <section className={className} aria-labelledby={titleId} aria-busy={busy}>
      <h2 id={titleId} className="attachments-title">{text.title}</h2>
      {files.length > 0 ? (
        <ul className="attachments-list">
          {files.map((file) => (
            <li key={file.id}>
              <span className="attachments-name">{file.originalFileName}</span>{" "}
              <span className="attachments-size" style={{ color: "var(--muted-foreground)" }}>{formatSize(file.sizeBytes, language)}</span>{" "}
              <button type="button" onClick={() => void onDownload(file)} aria-label={`${text.download} ${file.originalFileName}`}>
                {text.download}
              </button>
              {currentUserId !== undefined && file.createdByUserId === currentUserId ? (
                <>
                  {" "}
                  <button type="button" disabled={busy} onClick={() => void onDelete(file)} aria-label={`${text.delete} ${file.originalFileName}`}>
                    {text.delete}
                  </button>
                </>
              ) : null}
            </li>
          ))}
        </ul>
      ) : loading ? null : (
        <p className="attachments-empty" style={{ color: "var(--muted-foreground)" }}>{text.empty}</p>
      )}
      {files.length < total ? (
        <button type="button" disabled={busy} onClick={() => setPages((n) => n + 1)}>
          {text.more}
        </button>
      ) : null}
      {canUpload ? (
        <p className="attachments-add">
          <label htmlFor={inputId}>{text.add}</label>{" "}
          <input id={inputId} ref={input} type="file" accept={accept} disabled={busy} onChange={(event) => void onFileChosen(event)} />
        </p>
      ) : null}
      <p className="attachments-status" role="status">
        {uploading !== null ? text.uploading.replace("{name}", uploading) : ""}
      </p>
      {error ? (
        <p
          className="attachments-error"
          role="alert"
          style={{ background: "var(--destructive)", color: "var(--destructive-foreground)", borderRadius: "var(--radius)", padding: "6px 10px" }}
        >
          {error}
        </p>
      ) : null}
    </section>
  );
}

function formatSize(bytes: number, language: string): string {
  if (bytes < 1024) return formatNumber(bytes, language, { style: "unit", unit: "byte", unitDisplay: "short" });
  if (bytes < 1024 * 1024) return formatNumber(bytes / 1024, language, { style: "unit", unit: "kilobyte", maximumFractionDigits: 1 });
  return formatNumber(bytes / (1024 * 1024), language, { style: "unit", unit: "megabyte", maximumFractionDigits: 1 });
}
