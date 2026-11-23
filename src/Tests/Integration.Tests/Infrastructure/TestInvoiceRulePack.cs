using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.RulePack;

namespace Integration.Tests.Infrastructure;

/// <summary>
/// A country rule pack for the module's own tests, for the made-up country <c>ZZ</c> (an ISO user-assigned code).
/// It is not a real law: two tax codes (<c>S</c> 20 percent, <c>Z</c> 0 percent with a note), a series per type and
/// year, and one content rule (an invoice needs the customer's tax number when it is over 1000).
/// </summary>
internal sealed class TestInvoiceRulePack : IInvoiceDocumentsRulePack
{
    public const string Country = "ZZ";

    public string CountryCode => Country;

    public int RetentionYears => 10;

    public string SeriesKey(DocumentType type, DateOnly issueDate) => $"{type}-{issueDate.Year}";

    public string FormatNumber(DocumentType type, DateOnly issueDate, long sequence)
        => $"{Prefix(type)}-{issueDate.Year}-{sequence:D4}";

    public DateOnly? DefaultDueDate(DocumentType type, DateOnly issueDate, int paymentTermDays)
        => type is DocumentType.Invoice or DocumentType.AdvanceInvoice ? issueDate.AddDays(paymentTermDays) : null;

    public TaxDecision DecideTax(TaxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.TaxCode switch
        {
            "S" => new TaxDecision(0.20m),
            "Z" => new TaxDecision(0m, "Zero rated"),
            _ => new TaxDecision(0m),
        };
    }

    public IReadOnlyList<RuleViolation> Validate(DocumentForRules document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var violations = new List<RuleViolation>();
        var gross = document.Lines.Sum(l => l.Net + l.Tax);
        if (document.Type == DocumentType.Invoice && gross > 1000m && string.IsNullOrWhiteSpace(document.Customer.TaxNumber))
        {
            violations.Add(new RuleViolation("customer-tax-number", "An invoice over 1000 needs the customer's tax number."));
        }

        return violations;
    }

    public DocumentLayoutAdditions GetLayoutAdditions(DocumentForRules document)
        => new(["Test pack header"], ["Kept for 10 years"]);

    private static string Prefix(DocumentType type) => type switch
    {
        DocumentType.Quote => "QU",
        DocumentType.ProForma => "PF",
        DocumentType.Invoice => "INV",
        DocumentType.CreditNote => "CN",
        DocumentType.AdvanceInvoice => "ADV",
        _ => "DOC",
    };
}
