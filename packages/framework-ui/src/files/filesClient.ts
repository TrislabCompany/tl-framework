// A client of the Files module's endpoints (`/api/v1/files`), for the components of this package and for
// the apps. The app passes its own `fetch`, the one that adds the user's token and tenant, so this client
// never handles a token itself.

/** Mirrors `Visibility` of `Trislab.Modules.Files.Contracts`; the API writes enums as their names. */
export type FileVisibility = "Public" | "Private";

/** Mirrors `FileAssetStatus` of `Trislab.Modules.Files.Contracts`. */
export type FileStatus = "PendingUpload" | "Available" | "Quarantined";

/** Mirrors `FileAssetDto`, the shape every read endpoint of the Files module returns. */
export type FileAsset = {
  id: string;
  ownerType: string;
  ownerId?: string | null;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  visibility: FileVisibility;
  status: FileStatus;
  scanStatus: number;
  createdAtUtc: string;
  /** A durable URL, for public files only; a private file is read through `downloadUrl`. */
  publicUrl?: string | null;
  /** The user who uploaded the file. */
  createdByUserId: string;
  deletedOnUtc?: string | null;
  deletedBy?: string | null;
};

/** Mirrors `PagedResponse<T>`. */
export type FilesPage = {
  items: FileAsset[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

export type UploadFileOptions = {
  /** The owner type, such as `TestItem`: the owning module's `IFileAccessPolicy` decides who may attach. */
  ownerType: string;
  /** The record the file belongs to. */
  ownerId: string;
  /** A key of the API's `Files:Categories` (such as `Document` or `Image`), which sets the allowed extensions and size. */
  category: string;
  /** Default `Private`. */
  visibility?: FileVisibility;
};

export type FilesClientOptions = {
  /** The app's fetch, which adds the user's token and tenant. Default the browser's. */
  fetch?: typeof fetch;
  /** The API's origin, such as `https://api.example.com`. Default the page's own (`""`). */
  baseUrl?: string;
  /**
   * The fetch that PUTs the bytes to storage. Default the browser's: the presigned URL carries its own
   * signature, and the app's fetch would add a token storage must never see.
   */
  storageFetch?: typeof fetch;
};

export type FilesClient = {
  /** One page of an owner's files, newest first, as the owner type's policy lets the user see them. */
  listOwnerFiles: (ownerType: string, ownerId: string, page?: { pageNumber?: number; pageSize?: number }) => Promise<FilesPage>;
  /** Uploads a file to an owner: asks for a presigned URL, PUTs the bytes to storage, then finalizes it. */
  upload: (file: File, options: UploadFileOptions) => Promise<FileAsset>;
  /** A short-lived URL to download a file. */
  downloadUrl: (fileId: string) => Promise<string>;
  /** Moves a file to the trash, from where it can be restored for a while. */
  remove: (fileId: string) => Promise<void>;
};

/** A failed request to the Files API or to storage. `traceId` is the one the user quotes to support. */
export class FilesRequestError extends Error {
  readonly status: number;

  readonly traceId?: string;

  constructor(status: number, message: string, traceId?: string) {
    super(message);
    this.name = "FilesRequestError";
    this.status = status;
    this.traceId = traceId;
  }
}

type PresignedUpload = { fileAssetId: string; uploadUrl: string; requiredHeaders: Record<string, string> };

type ProblemDetails = { title?: string; detail?: string; traceId?: string };

export function createFilesClient(options: FilesClientOptions = {}): FilesClient {
  const apiFetch = options.fetch ?? ((input, init) => globalThis.fetch(input, init));
  const storageFetch = options.storageFetch ?? ((input, init) => globalThis.fetch(input, init));
  const base = `${(options.baseUrl ?? "").replace(/\/+$/, "")}/api/v1/files`;

  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers);
    headers.set("Accept", "application/json");
    if (init.body !== undefined) headers.set("Content-Type", "application/json");
    const response = await apiFetch(`${base}${path}`, { ...init, headers });
    if (!response.ok) throw await toError(response);
    if (response.status === 204) return undefined as T; // no body: the caller's T is void
    return (await response.json()) as T; // the API's documented shape, typed above
  }

  return {
    listOwnerFiles(ownerType, ownerId, page = {}) {
      const query = new URLSearchParams({
        ownerType,
        ownerId,
        pageNumber: String(page.pageNumber ?? 1),
        pageSize: String(page.pageSize ?? 20),
      });
      return request<FilesPage>(`?${query.toString()}`);
    },

    async upload(file, upload) {
      const presigned = await request<PresignedUpload>("/upload-url", {
        method: "POST",
        body: JSON.stringify({
          ownerType: upload.ownerType,
          ownerId: upload.ownerId,
          fileName: file.name,
          contentType: file.type || "application/octet-stream",
          sizeBytes: file.size,
          visibility: upload.visibility ?? "Private",
          category: upload.category,
        }),
      });
      const stored = await storageFetch(presigned.uploadUrl, {
        method: "PUT",
        headers: presigned.requiredHeaders,
        body: file,
      });
      if (!stored.ok) throw new FilesRequestError(stored.status, `Storage refused the upload (${stored.status}).`);
      return request<FileAsset>(`/${encodeURIComponent(presigned.fileAssetId)}/finalize`, { method: "POST" });
    },

    async downloadUrl(fileId) {
      const { url } = await request<{ url: string }>(`/${encodeURIComponent(fileId)}/url`);
      return url;
    },

    remove(fileId) {
      return request<void>(`/${encodeURIComponent(fileId)}`, { method: "DELETE" });
    },
  };
}

async function toError(response: Response): Promise<FilesRequestError> {
  let problem: ProblemDetails = {};
  try {
    problem = (await response.json()) as ProblemDetails; // ProblemDetails, or not JSON at all
  } catch {
    // Not JSON: the status says enough.
  }
  return new FilesRequestError(
    response.status,
    problem.detail ?? problem.title ?? `The Files API answered ${response.status}.`,
    problem.traceId,
  );
}
