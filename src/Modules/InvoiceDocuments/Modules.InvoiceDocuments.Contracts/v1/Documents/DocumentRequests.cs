using Mediator;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;

/// <summary>One line of a draft. When <c>ItemId</c> is set, an empty field takes the item's value.</summary>
public sealed record DocumentLineInput(
    Guid? ItemId,
    string? Description,
    decimal Quantity,
    string? Unit,
    decimal? UnitPrice,
    string? TaxCode);

/// <summary>
/// Creates a draft. A credit note needs <c>ReferenceDocumentId</c>, the issued invoice it corrects.
/// The currency defaults to the one in the issuer profile.
/// </summary>
public sealed record CreateDocumentDraftCommand(
    DocumentType Type,
    Guid CustomerId,
    string? Currency,
    DateOnly? DueDate,
    string? Notes,
    Guid? ReferenceDocumentId,
    IReadOnlyList<DocumentLineInput> Lines) : ICommand<Guid>;

public sealed record UpdateDocumentDraftCommand(
    Guid DocumentId,
    Guid CustomerId,
    DateOnly? DueDate,
    string? Notes,
    IReadOnlyList<DocumentLineInput> Lines) : ICommand<Guid>;

public sealed record DeleteDocumentDraftCommand(Guid DocumentId) : ICommand<Guid>;

/// <summary>
/// Gives the draft its number and locks it. <c>IssueDate</c> defaults to today.
/// </summary>
public sealed record IssueDocumentCommand(Guid DocumentId, DateOnly? IssueDate = null) : ICommand<Guid>;

/// <summary>Marks an issued invoice or advance invoice paid, by hand. <c>PaidOnUtc</c> defaults to now.</summary>
public sealed record MarkDocumentPaidCommand(Guid DocumentId, DateTime? PaidOnUtc = null) : ICommand<Guid>;

/// <summary>
/// Turns an issued quote or pro forma into a new draft of another type (a quote to a pro forma or an invoice,
/// a pro forma to an invoice). The source becomes <c>Converted</c>. Returns the id of the new draft.
/// </summary>
public sealed record ConvertDocumentCommand(Guid DocumentId, DocumentType TargetType) : ICommand<Guid>;

/// <summary>Sends an issued document to the customer by e-mail, with its PDF attached.</summary>
public sealed record SendDocumentCommand(Guid DocumentId, string? ToEmail = null, string? Message = null) : ICommand<Guid>;

public sealed record GetDocumentByIdQuery(Guid DocumentId) : IQuery<DocumentDto>;

/// <summary>
/// The list of documents. <c>OpenOnly</c> keeps issued invoices not yet paid; <c>OverdueOnly</c> keeps those past
/// their due date.
/// </summary>
public sealed record GetDocumentsQuery(
    DocumentType? Type = null,
    DocumentStatus? Status = null,
    Guid? CustomerId = null,
    bool OpenOnly = false,
    bool OverdueOnly = false,
    DateOnly? From = null,
    DateOnly? To = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<DocumentSummaryDto>>;

public sealed record GetDocumentPdfQuery(Guid DocumentId) : IQuery<DocumentFileDto>;

/// <summary>
/// For the accountant: a zip with <c>documents.csv</c> and one PDF per issued document, for documents
/// issued from <c>From</c> to <c>To</c> inclusive.
/// </summary>
public sealed record ExportDocumentsQuery(DateOnly From, DateOnly To) : IQuery<DocumentFileDto>;
