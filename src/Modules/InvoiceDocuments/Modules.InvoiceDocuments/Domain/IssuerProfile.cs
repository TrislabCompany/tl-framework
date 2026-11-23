using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>
/// The tenant's own business details, one row per tenant. Its country selects the country rule pack.
/// </summary>
public sealed class IssuerProfile : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public string AddressLine { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string CountryCode { get; private set; } = default!;
    public string? TaxNumber { get; private set; }
    public string? Iban { get; private set; }
    public string? Email { get; private set; }
    public string DefaultCurrency { get; private set; } = default!;
    public int DefaultPaymentTermDays { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private IssuerProfile() { }

    public static IssuerProfile Create(
        string name, string addressLine, string postalCode, string city, string countryCode,
        string? taxNumber, string? iban, string? email, string defaultCurrency, int defaultPaymentTermDays)
    {
        var profile = new IssuerProfile { Id = Guid.CreateVersion7() };
        profile.Update(name, addressLine, postalCode, city, countryCode, taxNumber, iban, email, defaultCurrency, defaultPaymentTermDays);
        return profile;
    }

    public void Update(
        string name, string addressLine, string postalCode, string city, string countryCode,
        string? taxNumber, string? iban, string? email, string defaultCurrency, int defaultPaymentTermDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        if (defaultPaymentTermDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPaymentTermDays), "The payment term cannot be negative.");
        }

        Name = name.Trim();
        AddressLine = (addressLine ?? string.Empty).Trim();
        PostalCode = (postalCode ?? string.Empty).Trim();
        City = (city ?? string.Empty).Trim();
        CountryCode = countryCode.Trim().ToUpperInvariant();
        TaxNumber = string.IsNullOrWhiteSpace(taxNumber) ? null : taxNumber.Trim();
        Iban = string.IsNullOrWhiteSpace(iban) ? null : iban.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        DefaultCurrency = defaultCurrency.Trim().ToUpperInvariant();
        DefaultPaymentTermDays = defaultPaymentTermDays;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public DocumentParty ToParty() => new(Name, AddressLine, PostalCode, City, CountryCode, TaxNumber);
}
