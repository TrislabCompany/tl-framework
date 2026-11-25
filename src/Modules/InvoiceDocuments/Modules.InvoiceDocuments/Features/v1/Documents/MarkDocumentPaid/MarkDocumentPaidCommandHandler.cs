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

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.MarkDocumentPaid;

public sealed class MarkDocumentPaidCommandHandler(
    InvoiceDocumentsDbContext dbContext,
    IOutboxWriter outbox,
    ITenantAccessor tenantAccessor)
    : ICommandHandler<MarkDocumentPaidCommand, Guid>
{
    public async ValueTask<Guid> Handle(MarkDocumentPaidCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct => await MarkPaidOnceAsync(command, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
        return command.DocumentId;
    }

    private async Task MarkPaidOnceAsync(MarkDocumentPaidCommand command, CancellationToken ct)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var document = await dbContext.Documents.FirstOrDefaultAsync(d => d.Id == command.DocumentId, ct).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);
        if (!Document.IsPayable(document.Type) || document.Status != DocumentStatus.Issued)
        {
            throw DocumentErrors.NotPayable();
        }

        var paidAt = command.PaidOnUtc ?? DateTime.UtcNow;
        document.MarkPaid(paidAt);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await outbox.AddAsync(new InvoiceDocumentPaidIntegrationEvent(
            Id: Guid.NewGuid(),
            OccurredOnUtc: DateTime.UtcNow,
            TenantId: tenantAccessor.TenantInfo?.Id,
            CorrelationId: Guid.NewGuid().ToString(),
            Source: "InvoiceDocuments",
            DocumentId: document.Id,
            DocumentType: document.Type,
            Number: document.Number!,
            CustomerId: document.CustomerId,
            Currency: document.Currency,
            TotalGross: document.TotalGross,
            PaidAtUtc: paidAt), ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }
}
