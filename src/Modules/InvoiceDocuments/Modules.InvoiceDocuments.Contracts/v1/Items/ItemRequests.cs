using Mediator;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Trislab.Modules.InvoiceDocuments.Contracts.v1.Items;

public sealed record CreateItemCommand(
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string TaxCode) : ICommand<Guid>;

public sealed record UpdateItemCommand(
    Guid ItemId,
    string Name,
    string? Description,
    string? Unit,
    decimal UnitPrice,
    string TaxCode) : ICommand<Guid>;

/// <summary>Removes an item from the lists. Documents already issued keep their own copy of its lines.</summary>
public sealed record DeleteItemCommand(Guid ItemId) : ICommand<Guid>;

public sealed record GetItemsQuery(
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<ItemDto>>;
