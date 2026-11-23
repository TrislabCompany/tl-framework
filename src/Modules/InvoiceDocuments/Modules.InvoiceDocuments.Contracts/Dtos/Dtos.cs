namespace Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

/// <summary>A customer of the tenant: the party a document is issued to.</summary>
public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber,
    string? Note);

/// <summary>
/// A product or service the tenant sells. <c>TaxCode</c> is a code the country rule pack understands
/// (the core does not know any rate).
/// </summary>
public sealed record ItemDto(
    Guid Id,
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string TaxCode);

/// <summary>
/// The tenant's own business details, printed on every document. <c>CountryCode</c> selects the country rule pack.
/// </summary>
public sealed record IssuerProfileDto(
    string Name,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber,
    string? Iban,
    string? Email,
    string DefaultCurrency,
    int DefaultPaymentTermDays);

public sealed record DocumentLineDto(
    int Position,
    Guid? ItemId,
    string Description,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxRate,
    string? TaxNote,
    decimal Net,
    decimal Tax,
    decimal Gross);

public sealed record DocumentPartyDto(
    string Name,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber);

/// <summary>
/// A document with its lines. After issue, <c>Issuer</c>, <c>Customer</c> and the lines are the copy taken at issue
/// time, so the document reads the same forever.
/// </summary>
public sealed record DocumentDto(
    Guid Id,
    DocumentType Type,
    DocumentStatus Status,
    string? Number,
    Guid CustomerId,
    DocumentPartyDto Customer,
    DocumentPartyDto? Issuer,
    string? IssuerIban,
    string Currency,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateTime? PaidAtUtc,
    DateTime? SentAtUtc,
    string? Notes,
    Guid? ReferenceDocumentId,
    Guid? ConvertedToDocumentId,
    decimal TotalNet,
    decimal TotalTax,
    decimal TotalGross,
    IReadOnlyList<string> HeaderLines,
    IReadOnlyList<string> FooterLines,
    IReadOnlyList<DocumentLineDto> Lines,
    DateTime CreatedAtUtc);

/// <summary>A row of the document list.</summary>
public sealed record DocumentSummaryDto(
    Guid Id,
    DocumentType Type,
    DocumentStatus Status,
    string? Number,
    Guid CustomerId,
    string CustomerName,
    string Currency,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    DateTime? PaidAtUtc,
    decimal TotalGross,
    bool IsOverdue);

/// <summary>A file the API returns: a PDF or an export.</summary>
public sealed record DocumentFileDto(byte[] Content, string FileName, string ContentType);
