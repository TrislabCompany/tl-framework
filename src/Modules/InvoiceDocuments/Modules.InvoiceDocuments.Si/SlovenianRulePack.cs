using System.Globalization;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.RulePack;

namespace Trislab.Modules.InvoiceDocuments.Si;

/// <summary>
/// Slovenian law for the documents of InvoiceDocuments (ZDDV-1): VAT rates and exemptions, the reverse charge,
/// the content of an invoice, the numbering series and the retention period.
/// </summary>
/// <remarks>
/// Per-tenant settings come from the tenant's business details: an issuer whose tax number has no <c>SI</c> prefix
/// is not registered for VAT and charges none. The exemption reason is part of the tax code of the item or line
/// (<see cref="SiVat.ExemptPrefix"/>). The legal text is in Slovenian on purpose: it is what the document must say.
/// </remarks>
public sealed class SlovenianRulePack : IInvoiceDocumentsRulePack
{
    public const string Country = "SI";

    /// <summary>An issued document is kept 10 years after the end of the year it relates to (ZDDV-1, article 86).</summary>
    public const int RetentionYearsDefault = 10;

    /// <summary>Invoices for real estate are kept 20 years. The core asks for one number, so a product that
    /// issues them reads this value itself.</summary>
    public const int RetentionYearsRealEstate = 20;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public string CountryCode => Country;

    public int RetentionYears => RetentionYearsDefault;

    public string SeriesKey(DocumentType type, DateOnly issueDate) => $"{Prefix(type)}-{issueDate.Year.ToString(Culture)}";

    public string FormatNumber(DocumentType type, DateOnly issueDate, long sequence)
        => $"{Prefix(type)}-{issueDate.Year.ToString(Culture)}-{sequence.ToString("D5", Culture)}";

    public DateOnly? DefaultDueDate(DocumentType type, DateOnly issueDate, int paymentTermDays)
        => type == DocumentType.Quote ? null : issueDate.AddDays(paymentTermDays);

    public TaxDecision DecideTax(TaxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A seller who is not registered for VAT charges none, whatever the code says.
        if (!IsVatRegistered(request.Issuer))
        {
            return new TaxDecision(0m, SiVat.NotVatRegisteredNote);
        }

        var code = request.TaxCode.Trim();
        if (SiVat.RateOf(code, request.Date) is { } rate)
        {
            return new TaxDecision(rate);
        }

        return code switch
        {
            SiVat.ReverseCharge => new TaxDecision(0m, SiVat.ReverseChargeNote),
            SiVat.Zero => new TaxDecision(0m, SiVat.ZeroRateNote),
            _ when SiVat.IsExempt(code) => new TaxDecision(0m, $"Oprostitev DDV: {SiVat.ExemptionReason(code)}."),

            // An unknown code is refused by Validate; here it charges nothing.
            _ => new TaxDecision(0m),
        };
    }

    public IReadOnlyList<RuleViolation> Validate(DocumentForRules document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var violations = new List<RuleViolation>();

        Require(violations, document.Issuer.Name, "si-issuer-name", "The issuer's name is required.");
        Require(violations, document.Issuer.AddressLine, "si-issuer-address", "The issuer's address is required.");
        Require(violations, document.Customer.Name, "si-customer-name", "The customer's name is required.");

        if (document.IssueDate < SiVat.FirstSupportedDate)
        {
            violations.Add(new RuleViolation("si-date", $"Documents before {SiVat.FirstSupportedDate:yyyy-MM-dd} are not supported."));
        }

        foreach (var line in document.Lines.Where(l => !SiVat.IsKnown(l.TaxCode.Trim())))
        {
            violations.Add(new RuleViolation("si-tax-code", $"Unknown tax code '{line.TaxCode}'."));
        }

        // A quote or a pro forma invoice is not a tax document: the rest is for the documents that are.
        if (document.Type is DocumentType.Quote or DocumentType.ProForma)
        {
            return violations;
        }

        Require(violations, document.Issuer.PostalCode, "si-issuer-address", "The issuer's postal code is required.");
        Require(violations, document.Customer.AddressLine, "si-customer-address", "The customer's address is required.");

        if (!SiTaxNumber.TryParse(document.Issuer.TaxNumber, out _, out _))
        {
            violations.Add(new RuleViolation("si-issuer-tax-number", "The issuer's tax number is required and must be a valid Slovenian tax number."));
        }

        if (!string.IsNullOrWhiteSpace(document.Customer.TaxNumber)
            && string.Equals(document.Customer.CountryCode, Country, StringComparison.OrdinalIgnoreCase)
            && !SiTaxNumber.TryParse(document.Customer.TaxNumber, out _, out _))
        {
            violations.Add(new RuleViolation("si-customer-tax-number", "The customer's Slovenian tax number is not valid."));
        }

        if (document.Lines.Any(l => l.TaxCode.Trim() == SiVat.ReverseCharge) && string.IsNullOrWhiteSpace(document.Customer.TaxNumber))
        {
            violations.Add(new RuleViolation("si-reverse-charge-customer", "The reverse charge needs the customer's VAT number."));
        }

        if (document.Type == DocumentType.CreditNote && string.IsNullOrWhiteSpace(document.ReferenceNumber))
        {
            violations.Add(new RuleViolation("si-credit-note-reference", "A credit note must refer to the number of the invoice it corrects."));
        }

        return violations;
    }

    public DocumentLayoutAdditions GetLayoutAdditions(DocumentForRules document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var header = new List<string>();
        var footer = new List<string>();

        if (document.Type == DocumentType.CreditNote && !string.IsNullOrWhiteSpace(document.ReferenceNumber))
        {
            header.Add($"Dobropis k računu št. {document.ReferenceNumber}");
        }

        if (document.Type == DocumentType.ProForma)
        {
            footer.Add("Predračun ni račun in ni podlaga za odbitek DDV.");
        }

        // The taxable amount and the VAT for each rate, as an invoice must show them.
        if (document.Type is not (DocumentType.Quote or DocumentType.ProForma))
        {
            foreach (var group in document.Lines.GroupBy(l => l.TaxRate).OrderByDescending(g => g.Key))
            {
                footer.Add(string.Create(Culture,
                    $"DDV {group.Key * 100:0.##} %: osnova {group.Sum(l => l.Net):N2} {document.Currency}, DDV {group.Sum(l => l.Tax):N2} {document.Currency}"));
            }
        }

        // Each legal note once, even when several lines carry it.
        if (document.Type != DocumentType.Quote)
        {
            footer.AddRange(LegalNotes(document));
        }

        return new DocumentLayoutAdditions(header, footer);
    }

    private static IEnumerable<string> LegalNotes(DocumentForRules document)
    {
        if (!IsVatRegistered(document.Issuer))
        {
            yield return SiVat.NotVatRegisteredNote;
        }

        foreach (var code in document.Lines.Select(l => l.TaxCode.Trim()).Distinct(StringComparer.Ordinal))
        {
            if (code == SiVat.ReverseCharge)
            {
                yield return SiVat.ReverseChargeNote;
            }
            else if (SiVat.IsExempt(code))
            {
                yield return $"Oprostitev DDV: {SiVat.ExemptionReason(code)}.";
            }
        }
    }

    /// <summary>A seller whose tax number carries the <c>SI</c> prefix is registered for VAT.</summary>
    public static bool IsVatRegistered(PartyForRules issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        return SiTaxNumber.TryParse(issuer.TaxNumber, out _, out var hasVatPrefix) && hasVatPrefix;
    }

    private static void Require(List<RuleViolation> violations, string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            violations.Add(new RuleViolation(code, message));
        }
    }

    private static string Prefix(DocumentType type) => type switch
    {
        DocumentType.Quote => "PON",
        DocumentType.ProForma => "PRE",
        DocumentType.Invoice => "RAC",
        DocumentType.CreditNote => "DOB",
        DocumentType.AdvanceInvoice => "AVA",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown document type."),
    };
}
