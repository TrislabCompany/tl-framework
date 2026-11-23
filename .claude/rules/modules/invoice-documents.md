---
paths:
  - "src/Modules/InvoiceDocuments/**"
  - "src/Tests/InvoiceDocuments.Tests/**"
  - "src/Tests/Integration.Tests/Tests/InvoiceDocuments/**"
---

# Module: InvoiceDocuments

The documents a tenant issues to its own customers (the capability `invoice-documents`): quotes, pro formas, invoices, credit notes and advance invoices, with the tenant's customers and items, numbering that never changes, a PDF, e-mail, marked paid by hand, and an export for the accountant. **Not** how a product charges its own tenants: that is SubscriptionsUsage. Module `Order = 520`; routes `/api/v1/invoice-documents`; schema `invoicedocuments`. `TenancyModes.All`: it works in both modes and references no multi-tenant-only module.

**Entities / DbContext:** `Customer`, `Item` (both `ISoftDeletable`), `IssuerProfile` (one row per tenant), `NumberingSeries`, `Document` (+ `DocumentLine`). `InvoiceDocumentsDbContext : BaseDbContext`, so every row is stamped and filtered by tenant in both modes. Enums are stored as strings.
**Areas:** Settings (the issuer profile), Customers, Items, Documents (draft create/update/delete, issue, mark paid, convert, send, get, list, pdf, export). Full list: `Features/v1/` or `/scalar`.

## The country rule pack

The core holds **no law**. Every legal decision is asked of an `IInvoiceDocumentsRulePack` (in the `.Contracts` project, `RulePack/`) chosen by the **issuer profile's country code**: the tax rate and note of each line, the content checks, the numbering series and number format, the default due date, the extra text printed on the document, and the retention years. A pack is its own package (the Slovenian one, `invoice-documents-si`, is a later item) that references the Contracts project and registers `services.AddSingleton<IInvoiceDocumentsRulePack, MyPack>()`. A product installs the packs of the countries it serves. A tenant with no issuer profile, or whose country has no pack, gets 422 on creating a draft. The module's own tests use `TestInvoiceRulePack` (made-up country `ZZ`) in `Integration.Tests/Infrastructure/`. Never put a rate, a legal text or a numbering rule in the core.

## Gotchas

- **Draft -> Issued -> Paid, and Issued -> Converted** (a quote or pro forma). Only a draft changes or is deleted. Issued documents keep their number and a copy of the issuer's and the customer's details and of the lines, so later changes to a customer or item never change an issued document. A mistake in an issued invoice is corrected by a credit note.
- **Numbering is gap-free.** `NumberingSeries` (one counter per series key, named by the pack) has `LastNumber` as a concurrency token. `IssueDocumentCommandHandler` takes the number, locks the document and writes the outbox event in one transaction, inside an execution strategy (the context retries on failure); two documents issued at once race for the counter and the loser retries up to five times. Never take a number outside that transaction.
- **Tax is decided again at issue**, on the issue date, so the document carries what the pack says that day. Drafts show the tax of the day they were saved.
- **Credit note:** refers to an issued or paid invoice of the same customer; its currency is the invoice's; the credit notes of an invoice together stay within the invoice's gross total (checked at issue). Amounts are positive. Only invoices and advance invoices are paid.
- **Events** `InvoiceDocumentIssuedIntegrationEvent` and `InvoiceDocumentPaidIntegrationEvent` are in the Contracts project and published through the outbox, in the same transaction as the change.
- **The export** is a zip: `documents.csv` (text cells that start with `=`, `+`, `-` or `@` are prefixed with `'`) and `pdf/<number>.pdf` for each document issued in the period.
- **Left out on purpose (P2 in the proposal):** document languages, currency handling beyond one currency code per document, recurring documents, deducting an advance invoice from the final invoice, a customer portal, payment through a provider, e-invoice formats (a pack's job).
