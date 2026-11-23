using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;

public sealed class CreateDocumentDraftCommandHandler(InvoiceDocumentsDbContext dbContext, DocumentSupport support)
    : ICommandHandler<CreateDocumentDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateDocumentDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var issuer = await support.GetIssuerProfileAsync(cancellationToken).ConfigureAwait(false);
        var pack = support.GetPack(issuer);
        var customer = await support.GetCustomerAsync(command.CustomerId, cancellationToken).ConfigureAwait(false);

        var currency = command.Currency;
        Guid? referenceId = null;
        if (command.Type == DocumentType.CreditNote)
        {
            var invoice = await dbContext.Documents.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == command.ReferenceDocumentId, cancellationToken).ConfigureAwait(false);
            if (invoice is null || invoice.Type != DocumentType.Invoice
                || invoice.Status is not (DocumentStatus.Issued or DocumentStatus.Paid)
                || invoice.CustomerId != command.CustomerId)
            {
                throw DocumentErrors.CreditNoteReferenceInvalid();
            }

            referenceId = invoice.Id;
            currency = invoice.Currency;
        }

        currency = string.IsNullOrWhiteSpace(currency) ? issuer.DefaultCurrency : currency;
        var today = DocumentMappings.Today();
        var dueDate = command.DueDate ?? pack.DefaultDueDate(command.Type, today, issuer.DefaultPaymentTermDays);
        var lines = await support
            .BuildLinesAsync(command.Lines, command.Type, today, issuer.ToParty(), customer.ToParty(), pack, cancellationToken)
            .ConfigureAwait(false);

        var document = Document.CreateDraft(
            command.Type, customer.Id, customer.ToParty(), currency, dueDate, command.Notes, referenceId, lines);
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document.Id;
    }
}
