---
paths:
  - "src/Modules/InvoiceDocuments/Modules.InvoiceDocuments.Si/**"
  - "src/Tests/InvoiceDocumentsSi.Tests/**"
---

# Pack: InvoiceDocuments.Si (the Slovenian rule pack)

The country rule pack of InvoiceDocuments for tenants whose country is `SI` (`docs/modules.md` section 7). It is a package, not a module: no `IModule`, no DbContext, no migrations, no endpoints. It references only `Modules.InvoiceDocuments.Contracts` and implements `IInvoiceDocumentsRulePack`; the host installs it with `services.AddInvoiceDocumentsSi()`.

## Gotchas

- **All law is data or small functions here, never in the core.** Rates and their dates are in `SiVat.cs`; a rate change adds a row with a `From` date, it never edits an old row (documents of an earlier date must keep their rate).
- **Tax codes:** `S`, `R1`, `R2`, `Z`, `RC`, `E:<reference>`. An unknown code charges nothing in `DecideTax` and is refused by `Validate` (`si-tax-code`); `DecideTax` never throws on user data.
- **VAT registration is read from the issuer's tax number:** the `SI` prefix means registered. Without it `DecideTax` charges no VAT and adds the article 94(1) note.
- **The legal text is Slovenian on purpose**, the rule messages are English (they are API errors).
- **Numbers:** `PREFIX-YYYY-NNNNN`, one series per type and year; the series key and the number format must stay in step, or two documents could share a number.
- **Not built yet:** UPN QR and eSLOG export (the core has no hook to draw an image or to add an export format), e-route or Peppol sending (P2), and the 20-year retention of real-estate invoices (`RetentionYears` is one number in the interface).
- Test with `InvoiceDocumentsSi.Tests`; every legal rule needs a test, and the rates and notes are reviewed with an accountant before a product goes live.
