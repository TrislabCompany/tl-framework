using Finbuckle.MultiTenant.Abstractions;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Events;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.IssueDocument;

/// <summary>
/// Issues a draft: asks the country rule pack to decide the tax and to check the content, takes the next number
/// of the series, copies the issuer's and the customer's details onto the document and locks it. The number, the
/// document and the "issued" event commit together or not at all, so numbers have no gaps. Two documents issued
/// at once race for the series counter (a concurrency token); the loser starts again.
/// </summary>
public sealed class IssueDocumentCommandHandler(
    InvoiceDocumentsDbContext dbContext,
    DocumentSupport support,
    IOutboxWriter outbox,
    IMultiTenantContextAccessor<AppTenantInfo> tenantAccessor)
    : ICommandHandler<IssueDocumentCommand, Guid>
{
    private const int MaxAttempts = 5;

    public async ValueTask<Guid> Handle(IssueDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var strategy = dbContext.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async ct => await IssueOnceAsync(command, ct).ConfigureAwait(false), cancellationToken)
                    .ConfigureAwait(false);
                return command.DocumentId;
            }
            catch (Exception ex) when (attempt < MaxAttempts && ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                // Another document took the number first, or created the series first: take the next one.
                dbContext.ChangeTracker.Clear();
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or DbUpdateException)
            {
                throw DocumentErrors.NumberingConflict();
            }
        }

        throw DocumentErrors.NumberingConflict();
    }

    private async Task IssueOnceAsync(IssueDocumentCommand command, CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var document = await dbContext.Documents.FirstOrDefaultAsync(d => d.Id == command.DocumentId, ct).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);
        if (document.Status != DocumentStatus.Draft)
        {
            throw DocumentErrors.DocumentNotDraft();
        }

        var issuer = await support.GetIssuerProfileAsync(ct).ConfigureAwait(false);
        var pack = support.GetPack(issuer);
        var issueDate = command.IssueDate ?? DocumentMappings.Today();

        // The customer's details as they are today; a customer deleted since keeps the copy on the draft.
        var customer = await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == document.CustomerId, ct).ConfigureAwait(false);
        var customerParty = customer?.ToParty() ?? document.Customer;
        var issuerParty = issuer.ToParty();

        // The tax is decided again on the day of issue, so the document carries what the pack says that day.
        var inputs = document.Lines
            .Select(l => new DocumentLineInput(l.ItemId, l.Description, l.Quantity, l.Unit, l.UnitPrice, l.TaxCode))
            .ToList();
        var lines = await support
            .BuildLinesAsync(inputs, document.Type, issueDate, issuerParty, customerParty, pack, ct).ConfigureAwait(false);
        document.Reprice(customerParty, lines);

        string? referenceNumber = null;
        if (document.Type == DocumentType.CreditNote)
        {
            referenceNumber = await CheckCreditNoteAsync(document, ct).ConfigureAwait(false);
        }

        var dueDate = document.DueDate ?? pack.DefaultDueDate(document.Type, issueDate, issuer.DefaultPaymentTermDays);
        var forRules = DocumentSupport.ToRules(document, issueDate, issuerParty, issuer.Iban, referenceNumber) with { DueDate = dueDate };
        var violations = pack.Validate(forRules);
        if (violations.Count > 0)
        {
            throw DocumentErrors.RuleViolations(violations.Select(v => v.Message));
        }

        var seriesKey = pack.SeriesKey(document.Type, issueDate);
        var series = await dbContext.NumberingSeries.FirstOrDefaultAsync(s => s.SeriesKey == seriesKey, ct).ConfigureAwait(false);
        if (series is null)
        {
            series = NumberingSeries.Start(seriesKey);
            dbContext.NumberingSeries.Add(series);
        }

        var number = pack.FormatNumber(document.Type, issueDate, series.Next());
        var layout = pack.GetLayoutAdditions(forRules);
        document.Issue(number, seriesKey, issueDate, dueDate, issuerParty, issuer.Iban, layout.HeaderLines, layout.FooterLines);

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await outbox.AddAsync(new InvoiceDocumentIssuedIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            TenantId: tenantAccessor.MultiTenantContext?.TenantInfo?.Id,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: "InvoiceDocuments",
            DocumentId: document.Id,
            DocumentType: document.Type,
            Number: number,
            CustomerId: document.CustomerId,
            Currency: document.Currency,
            TotalGross: document.TotalGross,
            IssueDate: issueDate,
            DueDate: dueDate), ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    /// <summary>A credit note refers to an issued or paid invoice, and all credit notes together stay within it.</summary>
    private async Task<string?> CheckCreditNoteAsync(Document creditNote, CancellationToken ct)
    {
        var invoice = await dbContext.Documents.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == creditNote.ReferenceDocumentId, ct).ConfigureAwait(false);
        if (invoice is null || invoice.Type != DocumentType.Invoice
            || invoice.Status is not (DocumentStatus.Issued or DocumentStatus.Paid))
        {
            throw DocumentErrors.CreditNoteReferenceInvalid();
        }

        var credited = await dbContext.Documents.AsNoTracking()
            .Where(d => d.ReferenceDocumentId == invoice.Id && d.Type == DocumentType.CreditNote && d.Status == DocumentStatus.Issued)
            .SumAsync(d => d.TotalGross, ct).ConfigureAwait(false);
        if (credited + creditNote.TotalGross > invoice.TotalGross)
        {
            throw DocumentErrors.CreditExceedsInvoice();
        }

        return invoice.Number;
    }
}
