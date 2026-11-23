using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.UpdateDocumentDraft;

public sealed class UpdateDocumentDraftCommandHandler(InvoiceDocumentsDbContext dbContext, DocumentSupport support)
    : ICommandHandler<UpdateDocumentDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateDocumentDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);
        if (document.Status != DocumentStatus.Draft)
        {
            throw DocumentErrors.DocumentNotDraft();
        }

        var issuer = await support.GetIssuerProfileAsync(cancellationToken).ConfigureAwait(false);
        var pack = support.GetPack(issuer);
        var customer = await support.GetCustomerAsync(command.CustomerId, cancellationToken).ConfigureAwait(false);
        var lines = await support
            .BuildLinesAsync(command.Lines, document.Type, DocumentMappings.Today(), issuer.ToParty(), customer.ToParty(), pack, cancellationToken)
            .ConfigureAwait(false);

        document.UpdateDraft(customer.Id, customer.ToParty(), command.DueDate, command.Notes, lines);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return document.Id;
    }
}
