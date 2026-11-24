import { describe, expect, it, vi } from "vitest";
import { FilesRequestError, createFilesClient } from "./filesClient";

type Call = { url: string; init?: RequestInit };

function fakeFetch(responses: Response[]) {
  const calls: Call[] = [];
  const fetch = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push({ url: String(input), init });
    const next = responses.shift();
    if (!next) throw new Error("no response left");
    return next;
  });
  return { fetch: fetch as unknown as typeof globalThis.fetch, calls };
}

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

describe("createFilesClient", () => {
  it("lists an owner's files with the query the API reads", async () => {
    const page = { items: [], pageNumber: 2, pageSize: 5, totalCount: 0, totalPages: 0 };
    const api = fakeFetch([json(page)]);
    const client = createFilesClient({ fetch: api.fetch, baseUrl: "https://api.example.com/" });

    expect(await client.listOwnerFiles("TestItem", "owner-1", { pageNumber: 2, pageSize: 5 })).toEqual(page);
    expect(api.calls[0]!.url).toBe("https://api.example.com/api/v1/files?ownerType=TestItem&ownerId=owner-1&pageNumber=2&pageSize=5");
  });

  it("uploads in three steps, and PUTs to storage without the app's fetch", async () => {
    const finalized = { id: "file-1", originalFileName: "a.pdf" };
    const api = fakeFetch([
      json({ fileAssetId: "file-1", uploadUrl: "https://storage.example.com/put?sig=1", requiredHeaders: { "Content-Type": "application/pdf" } }),
      json(finalized),
    ]);
    const storage = fakeFetch([new Response(null, { status: 200 })]);
    const client = createFilesClient({ fetch: api.fetch, storageFetch: storage.fetch });
    const file = new File(["%PDF"], "a.pdf", { type: "application/pdf" });

    expect(await client.upload(file, { ownerType: "TestItem", ownerId: "owner-1", category: "Document" })).toEqual(finalized);

    expect(api.calls[0]!.url).toBe("/api/v1/files/upload-url");
    expect(JSON.parse(String(api.calls[0]!.init?.body))).toEqual({
      ownerType: "TestItem",
      ownerId: "owner-1",
      fileName: "a.pdf",
      contentType: "application/pdf",
      sizeBytes: 4,
      visibility: "Private",
      category: "Document",
    });
    expect(storage.calls).toHaveLength(1);
    expect(storage.calls[0]!.url).toBe("https://storage.example.com/put?sig=1");
    expect(storage.calls[0]!.init?.method).toBe("PUT");
    expect(storage.calls[0]!.init?.headers).toEqual({ "Content-Type": "application/pdf" });
    expect(api.calls[1]!.url).toBe("/api/v1/files/file-1/finalize");
    expect(api.calls[1]!.init?.method).toBe("POST");
  });

  it("does not finalize when storage refuses the bytes", async () => {
    const api = fakeFetch([json({ fileAssetId: "file-1", uploadUrl: "https://storage.example.com/put", requiredHeaders: {} })]);
    const storage = fakeFetch([new Response(null, { status: 403 })]);
    const client = createFilesClient({ fetch: api.fetch, storageFetch: storage.fetch });

    await expect(client.upload(new File(["x"], "a.txt"), { ownerType: "T", ownerId: "o", category: "Document" }))
      .rejects.toMatchObject({ name: "FilesRequestError", status: 403 });
    expect(api.calls).toHaveLength(1);
  });

  it("gets a download URL and deletes a file", async () => {
    const api = fakeFetch([json({ url: "https://storage.example.com/get?sig=1", expiresAt: "" }), new Response(null, { status: 204 })]);
    const client = createFilesClient({ fetch: api.fetch });

    expect(await client.downloadUrl("file 1")).toBe("https://storage.example.com/get?sig=1");
    await client.remove("file 1");
    expect(api.calls.map((c) => c.url)).toEqual(["/api/v1/files/file%201/url", "/api/v1/files/file%201"]);
    expect(api.calls[1]!.init?.method).toBe("DELETE");
  });

  it("turns a ProblemDetails answer into an error with its trace id", async () => {
    const api = fakeFetch([json({ title: "Forbidden", detail: "Not allowed to list.", traceId: "00-abc-01" }, 403)]);
    const client = createFilesClient({ fetch: api.fetch });

    const error = await client.listOwnerFiles("T", "o").catch((e: unknown) => e);
    expect(error).toBeInstanceOf(FilesRequestError);
    expect(error).toMatchObject({ status: 403, message: "Not allowed to list.", traceId: "00-abc-01" });
  });
});
