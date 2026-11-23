using Mediator;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Items.CreateItem;

public sealed class CreateItemCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<CreateItemCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateItemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var item = Item.Create(command.Name, command.Description, command.Unit, command.UnitPrice, command.TaxCode);
        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return item.Id;
    }
}
