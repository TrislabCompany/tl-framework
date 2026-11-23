using Mediator;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;

public sealed record CreateCustomerCommand(
    string Name,
    string? Email,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber,
    string? Note) : ICommand<Guid>;

public sealed record UpdateCustomerCommand(
    Guid CustomerId,
    string Name,
    string? Email,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber,
    string? Note) : ICommand<Guid>;

/// <summary>Removes a customer from the lists. Documents already issued to it keep their own copy of its details.</summary>
public sealed record DeleteCustomerCommand(Guid CustomerId) : ICommand<Guid>;

public sealed record GetCustomersQuery(
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IQuery<PagedResponse<CustomerDto>>;
