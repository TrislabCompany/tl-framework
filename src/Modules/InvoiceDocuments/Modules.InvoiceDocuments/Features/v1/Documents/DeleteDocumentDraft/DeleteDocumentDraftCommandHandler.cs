using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.DeleteDocumentDraft;

public sealed class DeleteDocumentDraftCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<DeleteDocumentDraftCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteDocumentDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var document = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);

        // Only a draft is deleted. An issued document has a number and is kept; a credit note corrects it.
        if (document.Status != DocumentStatus.Draft)
        {
            throw DocumentErrors.DocumentNotDraft();
        }

        dbContext.Documents.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.DocumentId;
    }
}
