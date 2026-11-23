using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.DeleteItem;

public sealed class DeleteItemCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<DeleteItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == command.ItemId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.ItemNotFound(command.ItemId);

        item.Delete(deletedBy: null);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
