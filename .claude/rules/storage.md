---
paths:
  - "src/BuildingBlocks/Storage/**"
  - "src/Modules/Files/**"
---

# Storage & file uploads

`src/BuildingBlocks/Storage/`. Read before working with files/blobs.

## `IStorageService`

`UploadAsync<T>(FileUploadRequest, FileType, ct)`, `RemoveAsync(path, ct)`, `DownloadAsync`, `ExistsAsync`, `GetSizeAsync` (0 if absent), `GenerateUploadUrlAsync`/`GenerateDownloadUrlAsync` (presigned), `HeadObjectAsync`, `BuildPublicUrl(key)→string` (string, not Uri — local storage returns a server-relative path).

`FileType`: `Image` (5MB), `Document`, `Pdf` (10MB) — `FileTypeMetadata.GetRules` enforces extension + size. **Always propagate `CancellationToken`.**

## Providers

`AddHeroStorage(config)` reads `Storage:Provider` **eagerly at registration**: `"s3"` → `S3StorageService` (any S3-compatible service via `ServiceUrl` + `ForcePathStyle`: rustfs in a stamp, the AppHost and the tests), else `LocalStorageService`. Presigned URLs are signed by a second client, keyed `S3StorageService.PresignClientKey`, for `PresignServiceUrl` (`S3_PUBLIC_ENDPOINT`): the browser reaches the storage through the front end's `/storage` proxy, on the page's hostname. When quotas are enabled the service is wrapped in `QuotaMeteredStorageService` (debits `StorageBytes`).

`Storage:S3:PresignServiceUrl` (optional, validated at startup as an absolute http(s) URL): presigned PUT/GET URLs are signed for this host through a second, keyed `IAmazonS3` (`S3StorageService.PresignClientKey`); all real I/O stays on `ServiceUrl`. Needed when the API reaches the store on an internal address browsers can't resolve (compose: `http://rustfs:9000`). A test factory that re-registers the S3 stack must register that keyed client too (see `FshWebApplicationFactory`).

## Presigned upload flow (preferred for user uploads)

Don't stream large files through the API. The pattern (see Files module):
1. `RequestUploadUrl` — server validates category/extension/size + quota pre-check, returns a presigned PUT URL, persists a `PendingUpload` record.
2. Client uploads **directly** to storage.
3. `FinalizeUpload` — flips to `Available`, **debits the quota here** (not at request time), publishes `FileFinalizedIntegrationEvent`.

Without S3 storage, local disk uses `LocalPresignTokenStore` (in-memory one-shot tokens).

## Test gotcha

`AddHeroStorage` reads `Storage:Provider` **before** a test factory's in-memory config overlay applies, so it wires `LocalStorageService`. Integration tests that need object storage must **remove the `IStorageService`/`LocalStorageService`/`S3StorageService` descriptors post-registration and re-register the S3 stack** pointed at the rustfs container (see `FshWebApplicationFactory`). See `integration-testing.md`.
