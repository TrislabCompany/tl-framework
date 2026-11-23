using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Documents.ConvertDocument;

/// <summary>
/// Turns an issued quote or pro forma into a new draft of the target type with the same customer and lines
/// (priced again on the day). The source keeps its number and becomes <c>Converted</c>.
/// </summary>
public sealed class ConvertDocumentCommandHandler(InvoiceDocumentsDbContext dbContext, DocumentSupport support)
    : ICommandHandler<ConvertDocumentCommand, Guid>
{
    public async ValueTask<Guid> Handle(ConvertDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == command.DocumentId, cancellationToken).ConfigureAwait(false)
            ?? throw DocumentErrors.DocumentNotFound(command.DocumentId);
        if (source.Status != DocumentStatus.Issued)
        {
            throw DocumentErrors.NotIssued();
        }
        if (!Document.CanConvert(source.Type, command.TargetType))
        {
            throw DocumentErrors.ConversionNotAllowed(source.Type.ToString(), command.TargetType.ToString());
        }

        var issuer = await support.GetIssuerProfileAsync(cancellationToken).ConfigureAwait(false);
        var pack = support.GetPack(issuer);
        var today = DocumentMappings.Today();

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == source.CustomerId, cancellationToken).ConfigureAwait(false);
        var customerParty = customer?.ToParty() ?? source.Customer;
        var inputs = source.Lines
            .OrderBy(l => l.Position)
            .Select(l => new DocumentLineInput(l.ItemId, l.Description, l.Quantity, l.Unit, l.UnitPrice, l.TaxCode))
            .ToList();
        var lines = await support
            .BuildLinesAsync(inputs, command.TargetType, today, issuer.ToParty(), customerParty, pack, cancellationToken)
            .ConfigureAwait(false);

        var target = Document.CreateDraft(
            command.TargetType, source.CustomerId, customerParty, source.Currency,
            pack.DefaultDueDate(command.TargetType, today, issuer.DefaultPaymentTermDays),
            source.Notes, referenceDocumentId: null, lines);
        dbContext.Documents.Add(target);

        // The link back to the source is kept on the source (ConvertedToDocumentId).
        source.MarkConverted(target.Id);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return target.Id;
    }
}
