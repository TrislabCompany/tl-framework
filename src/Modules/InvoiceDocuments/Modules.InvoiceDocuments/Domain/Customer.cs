using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>A customer of the tenant: the party a document is issued to.</summary>
public sealed class Customer : AggregateRoot<Guid>, ISoftDeletable
{
    public string Name { get; private set; } = default!;
    public string? Email { get; private set; }
    public string AddressLine { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string City { get; private set; } = default!;

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string CountryCode { get; private set; } = default!;
    public string? TaxNumber { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    private Customer() { }

    public static Customer Create(
        string name, string? email, string addressLine, string postalCode, string city,
        string countryCode, string? taxNumber, string? note)
    {
        var customer = new Customer { Id = Guid.CreateVersion7(), CreatedAtUtc = DateTime.UtcNow };
        customer.Apply(name, email, addressLine, postalCode, city, countryCode, taxNumber, note);
        return customer;
    }

    public void Update(
        string name, string? email, string addressLine, string postalCode, string city,
        string countryCode, string? taxNumber, string? note)
    {
        Apply(name, email, addressLine, postalCode, city, countryCode, taxNumber, note);
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Delete(string? deletedBy)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedOnUtc = DateTimeOffset.UtcNow;
        DeletedBy = deletedBy;
    }

    public DocumentParty ToParty() => new(Name, AddressLine, PostalCode, City, CountryCode, TaxNumber);

    private void Apply(
        string name, string? email, string addressLine, string postalCode, string city,
        string countryCode, string? taxNumber, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        Name = name.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        AddressLine = (addressLine ?? string.Empty).Trim();
        PostalCode = (postalCode ?? string.Empty).Trim();
        City = (city ?? string.Empty).Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        Note = note?.Trim();
    }
}
