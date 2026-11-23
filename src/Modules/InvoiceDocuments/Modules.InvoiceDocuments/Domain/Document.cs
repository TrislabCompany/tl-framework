using Trislab.Framework.Core.Domain;
using Trislab.Modules.InvoiceDocuments.Contracts;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>
/// A quote, pro forma, invoice, credit note or advance invoice. State machine:
/// <c>Draft -> Issued -> Paid</c>, and <c>Issued -> Converted</c> for a quote or pro forma. A draft can change and be
/// deleted; once issued, the number, the parties and the lines never change.
/// </summary>
public sealed class Document : AggregateRoot<Guid>
{
    private readonly List<DocumentLine> _lines = [];

    public DocumentType Type { get; private set; }
    public DocumentStatus Status { get; private set; }

    /// <summary>Null while a draft; given once at issue and never changed.</summary>
    public string? Number { get; private set; }
    public string? SeriesKey { get; private set; }

    public Guid CustomerId { get; private set; }
    public DocumentParty Customer { get; private set; } = default!;

    /// <summary>The issuer's details, copied at issue time. Null while a draft.</summary>
    public DocumentParty? Issuer { get; private set; }
    public string? IssuerIban { get; private set; }

    public string Currency { get; private set; } = default!;
    public DateOnly? IssueDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateTime? IssuedAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The invoice a credit note corrects, or the document this one was converted from.</summary>
    public Guid? ReferenceDocumentId { get; private set; }
    public Guid? ConvertedToDocumentId { get; private set; }

    public decimal TotalNet { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal TotalGross { get; private set; }

    /// <summary>Text the country rule pack added above the lines, one per line, copied at issue time.</summary>
    public string? HeaderText { get; private set; }

    /// <summary>Text the country rule pack added below the lines, one per line, copied at issue time.</summary>
    public string? FooterText { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public IReadOnlyList<DocumentLine> Lines => _lines;

    private Document() { }

    public bool IsOverdue(DateOnly today)
        => Status == DocumentStatus.Issued && IsPayable(Type) && DueDate is { } due && due < today;

    /// <summary>Only invoices and advance invoices are paid; the others are not money owed.</summary>
    public static bool IsPayable(DocumentType type) => type is DocumentType.Invoice or DocumentType.AdvanceInvoice;

    public static Document CreateDraft(
        DocumentType type,
        Guid customerId,
        DocumentParty customer,
        string currency,
        DateOnly? dueDate,
        string? notes,
        Guid? referenceDocumentId,
        IEnumerable<DocumentLine> lines)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        if (type == DocumentType.CreditNote && referenceDocumentId is null)
        {
            throw new InvalidOperationException("A credit note needs the invoice it corrects.");
        }

        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            Type = type,
            Status = DocumentStatus.Draft,
            CustomerId = customerId,
            Customer = customer,
            Currency = currency.Trim().ToUpperInvariant(),
            DueDate = dueDate,
            Notes = notes?.Trim(),
            ReferenceDocumentId = referenceDocumentId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        document.SetLines(lines);
        return document;
    }

    /// <summary>Replaces the content of a draft.</summary>
    public void UpdateDraft(Guid customerId, DocumentParty customer, DateOnly? dueDate, string? notes, IEnumerable<DocumentLine> lines)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }

        CustomerId = customerId;
        Customer = customer;
        DueDate = dueDate;
        Notes = notes?.Trim();
        SetLines(lines);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the lines of a draft with lines whose tax was decided again, and refreshes the customer's
    /// details. Used just before issue, so the document carries what the law says on the day it is issued.
    /// </summary>
    public void Reprice(DocumentParty customer, IEnumerable<DocumentLine> lines)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(lines);
        Customer = customer;
        SetLines(lines);
    }

    /// <summary>Locks the draft: gives it its number, the issuer's details and the pack's extra text.</summary>
    public void Issue(
        string number,
        string seriesKey,
        DateOnly issueDate,
        DateOnly? dueDate,
        DocumentParty issuer,
        string? issuerIban,
        IReadOnlyList<string> headerLines,
        IReadOnlyList<string> footerLines)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(headerLines);
        ArgumentNullException.ThrowIfNull(footerLines);
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("A document needs at least one line.");
        }

        Number = number;
        SeriesKey = seriesKey;
        IssueDate = issueDate;
        DueDate = dueDate;
        Issuer = issuer;
        IssuerIban = issuerIban;
        HeaderText = headerLines.Count == 0 ? null : string.Join('\n', headerLines);
        FooterText = footerLines.Count == 0 ? null : string.Join('\n', footerLines);
        Status = DocumentStatus.Issued;
        IssuedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = IssuedAtUtc;
    }

    public void MarkPaid(DateTime paidAtUtc)
    {
        if (!IsPayable(Type))
        {
            throw new InvalidOperationException($"A {Type} is not paid.");
        }
        if (Status != DocumentStatus.Issued)
        {
            throw new InvalidOperationException($"Only an issued document can be marked paid, this one is {Status}.");
        }

        PaidAtUtc = paidAtUtc;
        Status = DocumentStatus.Paid;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>The conversion targets a document of this type may have.</summary>
    public static bool CanConvert(DocumentType from, DocumentType to) => (from, to) switch
    {
        (DocumentType.Quote, DocumentType.ProForma) => true,
        (DocumentType.Quote, DocumentType.Invoice) => true,
        (DocumentType.ProForma, DocumentType.Invoice) => true,
        _ => false,
    };

    public void MarkConverted(Guid targetId)
    {
        if (Status != DocumentStatus.Issued)
        {
            throw new InvalidOperationException($"Only an issued document can be converted, this one is {Status}.");
        }

        ConvertedToDocumentId = targetId;
        Status = DocumentStatus.Converted;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Records that the document was e-mailed. It is not a change of the document's content.</summary>
    public void MarkSent(DateTime sentAtUtc)
    {
        if (Status == DocumentStatus.Draft)
        {
            throw new InvalidOperationException("A draft cannot be sent.");
        }

        SentAtUtc = sentAtUtc;
    }

    public void EnsureDraft()
    {
        if (Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException($"An issued document cannot be changed, this one is {Status}.");
        }
    }

    private void SetLines(IEnumerable<DocumentLine> lines)
    {
        _lines.Clear();
        int position = 1;
        foreach (var line in lines)
        {
            line.SetPosition(position++);
            _lines.Add(line);
        }

        TotalNet = _lines.Sum(l => l.Net);
        TotalTax = _lines.Sum(l => l.Tax);
        TotalGross = _lines.Sum(l => l.Gross);
    }
}

/// <summary>A copy of a party's details stored on a document, so a later change of the customer never changes it.</summary>
public sealed record DocumentParty(
    string Name,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber);
