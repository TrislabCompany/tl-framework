---
paths:
  - "src/Modules/Files/**"
  - "src/Tests/Files.Tests/**"
---

# Module: Files

Presigned-URL file lifecycle (upload → finalize → serve → delete) shared by Catalog images, Chat attachments, avatars. Module `Order = 350` (loads before consumer modules).

**Entities / DbContext:** `FileAsset` (aggregate, soft-deletable): status `PendingUpload → Available | Quarantined`, `Visibility` (Public/Private), `ScanStatus`. `FilesDbContext`. Publishes `FileFinalizedIntegrationEvent`.
**Areas:** RequestUploadUrl, FinalizeUpload, GetFileDownloadUrl/Metadata, ChangeVisibility, Delete/Restore, ListMy/Shared/Trashed, ListOwnerFiles (`GET /files?ownerType=&ownerId=`, a record's attachments). Purge jobs (orphaned hourly, deleted daily). Full list: `Features/v1/` or `/scalar`. Storage mechanics: `storage.md`.

## Gotchas

- **Presigned flow** — never stream uploads through the API. RequestUploadUrl validates category/extension/size + quota **pre-check** and persists a `PendingUpload`; client uploads directly to storage; **FinalizeUpload debits the quota** (not at request time) and flips to Available/Quarantined.
- **`FileAccessPolicyRegistry`** resolves `IFileAccessPolicy` by **OwnerType** — case-insensitive, **closed by default** (unknown OwnerType → forbidden), **last-write-wins** on duplicates (intentional, for test substitution). Each owning module registers its own policy in its `ConfigureServices` (Catalog/Tickets load after Files). Files ships `DefaultUploaderOnlyPolicy` for built-in OwnerTypes `"MyFiles"`/`"User"`.
- **Visibility is in the storage key** (`public/…` vs `private/…`, built only by `StorageKeyBuilder`). Changing visibility moves the object via `FileStorageRelocator` and publishes `FileStorageKeyChangedIntegrationEvent`; a module that stores a FileAsset's public URL must handle it. `MigrateLegacyPublicFileKeysJob` (enqueued on each start) moves pre-#1410 public files. See `storage.md`.
- `CanChangeVisibilityAsync` defaults to the delete rule (uploader-only); domain-bound files (e.g. product images) may override to forbid visibility flips.
- Tenant scoping is implicit via `BaseDbContext` (no explicit `TenantId` on `FileAsset`).
- **ListOwnerFiles** is how a module shows a record's files without a link table of its own: `CanListAsync` gates the list (default: the attach rule; `DefaultUploaderOnlyPolicy` allows it only for the caller's own user id), then `CanReadAsync` filters it per file **before** paging, so pages are full. Only the owner's **500 newest** available files (`ListOwnerFilesQueryHandler.MaxCandidates`) are considered, so a request makes at most 500 policy calls, and `TotalCount` counts the readable files within that window. Pending, quarantined and deleted files are never listed. Unknown owner type or a denied list → 403.
- The endpoint is gated by `Files.Upload`, the **baseline file permission** every Basic user has (as for `/mine` and `/shared`); the owner type's policy decides the rest. `Attachments`' `canUpload` only hides the upload control.
- RequestUploadUrl stores the **policy's** spelling of the OwnerType (lookup is case-insensitive), and the owner list matches that spelling exactly.

To support uploads for a new owner type: implement `IFileAccessPolicy`, register it in the owning module, and use that OwnerType in RequestUploadUrl. Its files are then listed with ListOwnerFiles, and shown in a front end with `Attachments` from `@trislabcompany/framework-ui`.
