using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.UpdateItem;

public sealed class UpdateItemCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<UpdateItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = await dbContext.Items
            .FirstOrDefaultAsync(i => i.Id == command.ItemId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.ItemNotFound(command.ItemId);

        // Documents already issued keep their own copy of the line.
        item.Update(command.Name, command.Description, command.Unit, command.UnitPrice, command.TaxCode);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
