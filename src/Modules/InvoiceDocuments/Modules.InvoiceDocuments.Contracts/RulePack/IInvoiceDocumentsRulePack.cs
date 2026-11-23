namespace Trislab.Modules.InvoiceDocuments.Contracts.RulePack;

/// <summary>
/// The law of one country, behind one interface. The InvoiceDocuments core holds no law: VAT rates and
/// exemptions, the required content, the numbering rules and the extra text on a document are all asked of
/// the pack that matches the issuer's country. A pack is its own package (for example <c>invoice-documents-si</c>)
/// that references this Contracts project and registers itself with
/// <c>services.AddSingleton&lt;IInvoiceDocumentsRulePack, MyPack&gt;()</c>. A product installs the packs of the
/// countries it serves; a tenant whose country has no pack installed cannot create a document.
/// </summary>
public interface IInvoiceDocumentsRulePack
{
    /// <summary>The ISO 3166-1 alpha-2 country code in upper case, such as <c>SI</c>.</summary>
    string CountryCode { get; }

    /// <summary>How many years an issued document must be kept (retention). The core never deletes an issued document.</summary>
    int RetentionYears { get; }

    /// <summary>
    /// The numbering series a document belongs to. Documents with the same key share one gap-free counter,
    /// so the pack decides whether a series is per type, per year, or both (for example <c>invoice-2026</c>).
    /// </summary>
    string SeriesKey(DocumentType type, DateOnly issueDate);

    /// <summary>Writes the number printed on the document from the next value of its series.</summary>
    string FormatNumber(DocumentType type, DateOnly issueDate, long sequence);

    /// <summary>The due date to propose when the user gives none.</summary>
    DateOnly? DefaultDueDate(DocumentType type, DateOnly issueDate, int paymentTermDays);

    /// <summary>The tax rate and note for one line. Called for every line of every draft and again at issue.</summary>
    TaxDecision DecideTax(TaxRequest request);

    /// <summary>The legal content checks. An empty list means the document may be issued.</summary>
    IReadOnlyList<RuleViolation> Validate(DocumentForRules document);

    /// <summary>The extra text the law requires on the printed document (legal notes, payment details).</summary>
    DocumentLayoutAdditions GetLayoutAdditions(DocumentForRules document);
}

/// <summary>Asks for the tax of one line.</summary>
public sealed record TaxRequest(
    DocumentType DocumentType,
    DateOnly Date,
    string TaxCode,
    PartyForRules Issuer,
    PartyForRules Customer);

/// <summary>The tax rate as a fraction (0.22 is 22 percent) and a note to print when the rate needs an explanation.</summary>
public sealed record TaxDecision(decimal Rate, string? Note = null);

/// <summary>A reason a document may not be issued. <c>Code</c> is stable; <c>Message</c> is English.</summary>
public sealed record RuleViolation(string Code, string Message);

/// <summary>Text blocks the pack adds above and below the lines of the printed document.</summary>
public sealed record DocumentLayoutAdditions(IReadOnlyList<string> HeaderLines, IReadOnlyList<string> FooterLines)
{
    public static DocumentLayoutAdditions None { get; } = new([], []);
}

public sealed record PartyForRules(
    string Name,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber);

public sealed record LineForRules(
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxRate,
    decimal Net,
    decimal Tax);

/// <summary>What the pack sees of a document: no database types, only values.</summary>
public sealed record DocumentForRules(
    DocumentType Type,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string Currency,
    PartyForRules Issuer,
    string? IssuerIban,
    PartyForRules Customer,
    string? ReferenceNumber,
    IReadOnlyList<LineForRules> Lines);
