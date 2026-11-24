// Test-only: not built into dist (tsconfig.build.json). A FilesClient that keeps the files in memory, as
// the API would list them to the user `CURRENT_USER`, for the browser tests and the specimen page.
import type { FileAsset, FilesClient } from "../files/filesClient";

export const CURRENT_USER = "user-1";

export function fileAsset(id: string, name: string, sizeBytes: number, createdByUserId = CURRENT_USER): FileAsset {
  return {
    id,
    ownerType: "TestItem",
    ownerId: "owner-1",
    originalFileName: name,
    contentType: "application/pdf",
    sizeBytes,
    visibility: "Private",
    status: "Available",
    scanStatus: 0,
    createdAtUtc: "2026-11-24T10:00:00Z",
    publicUrl: null,
    createdByUserId,
  };
}

/** The files newest first, as the API lists them; `upload` adds one at the top. */
export function memoryFilesClient(initial: FileAsset[]): FilesClient & { files: FileAsset[] } {
  const files = [...initial];
  let next = files.length + 1;
  return {
    files,
    async listOwnerFiles(_ownerType, _ownerId, page = {}) {
      const pageNumber = page.pageNumber ?? 1;
      const pageSize = page.pageSize ?? 20;
      return {
        items: files.slice((pageNumber - 1) * pageSize, pageNumber * pageSize),
        pageNumber,
        pageSize,
        totalCount: files.length,
        totalPages: Math.ceil(files.length / pageSize),
      };
    },
    async upload(file) {
      const added = fileAsset(`file-${next++}`, file.name, file.size);
      files.unshift(added);
      return added;
    },
    async downloadUrl(fileId) {
      return `https://storage.example.com/${fileId}`;
    },
    async remove(fileId) {
      files.splice(files.findIndex((f) => f.id === fileId), 1);
    },
  };
}
