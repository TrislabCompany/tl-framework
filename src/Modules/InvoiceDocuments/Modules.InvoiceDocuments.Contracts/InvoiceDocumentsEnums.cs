namespace Trislab.Modules.InvoiceDocuments.Contracts;

/// <summary>The kind of document a tenant issues to its own customer.</summary>
public enum DocumentType
{
    Quote = 0,
    ProForma = 1,
    Invoice = 2,
    CreditNote = 3,
    AdvanceInvoice = 4,
}

/// <summary>
/// Where a document is in its life. A draft can change; an issued document has its number and never changes again.
/// </summary>
public enum DocumentStatus
{
    Draft = 0,
    Issued = 1,
    Paid = 2,

    /// <summary>A quote or pro forma that was turned into another document.</summary>
    Converted = 3,
}
