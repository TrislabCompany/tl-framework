using Mediator;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;

namespace Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;

/// <summary>The tenant's business details; <c>null</c> until the tenant has saved them once.</summary>
public sealed record GetIssuerProfileQuery : IQuery<IssuerProfileDto?>;

/// <summary>Creates or replaces the tenant's business details.</summary>
public sealed record UpdateIssuerProfileCommand(
    string Name,
    string AddressLine,
    string PostalCode,
    string City,
    string CountryCode,
    string? TaxNumber,
    string? Iban,
    string? Email,
    string DefaultCurrency,
    int DefaultPaymentTermDays) : ICommand<Guid>;
