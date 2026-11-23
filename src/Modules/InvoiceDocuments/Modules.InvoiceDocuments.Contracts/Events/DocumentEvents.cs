using Trislab.Framework.Eventing.Abstractions;

namespace Trislab.Modules.InvoiceDocuments.Contracts.Events;

/// <summary>
/// Raised when a document is issued and takes its number. Webhooks and other modules react to it.
/// </summary>
public sealed record InvoiceDocumentIssuedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid DocumentId,
    DocumentType DocumentType,
    string Number,
    Guid CustomerId,
    string Currency,
    decimal TotalGross,
    DateOnly IssueDate,
    DateOnly? DueDate)
    : IIntegrationEvent;

/// <summary>
/// Raised when an invoice or advance invoice is marked paid (by hand: there is no payment provider).
/// </summary>
public sealed record InvoiceDocumentPaidIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    string? TenantId,
    string CorrelationId,
    string Source,
    Guid DocumentId,
    DocumentType DocumentType,
    string Number,
    Guid CustomerId,
    string Currency,
    decimal TotalGross,
    DateTime PaidAtUtc)
    : IIntegrationEvent;
