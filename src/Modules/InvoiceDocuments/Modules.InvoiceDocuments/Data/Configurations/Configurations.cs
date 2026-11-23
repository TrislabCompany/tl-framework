using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trislab.Modules.InvoiceDocuments.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.AddressLine).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.TaxNumber).HasMaxLength(32);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);
        builder.HasIndex(x => x.Name);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Items");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.TaxCode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.DeletedBy).HasMaxLength(64);
        builder.HasIndex(x => x.Name);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class IssuerProfileConfiguration : IEntityTypeConfiguration<IssuerProfile>
{
    public void Configure(EntityTypeBuilder<IssuerProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("IssuerProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AddressLine).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PostalCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.City).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CountryCode).IsRequired().HasMaxLength(2);
        builder.Property(x => x.TaxNumber).HasMaxLength(32);
        builder.Property(x => x.Iban).HasMaxLength(34);
        builder.Property(x => x.Email).HasMaxLength(254);
        builder.Property(x => x.DefaultCurrency).IsRequired().HasMaxLength(3);
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class NumberingSeriesConfiguration : IEntityTypeConfiguration<NumberingSeries>
{
    public void Configure(EntityTypeBuilder<NumberingSeries> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("NumberingSeries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SeriesKey).IsRequired().HasMaxLength(64);

        // Two documents issued at once must not take the same number: the loser of the race retries.
        builder.Property(x => x.LastNumber).IsConcurrencyToken();

        // One counter per series. Finbuckle adds the tenant to this unique index.
        builder.HasIndex(x => x.SeriesKey).IsUnique();
        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("Documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Number).HasMaxLength(64);
        builder.Property(x => x.SeriesKey).HasMaxLength(64);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.IssuerIban).HasMaxLength(34);
        builder.Property(x => x.HeaderText).HasMaxLength(2000);
        builder.Property(x => x.FooterText).HasMaxLength(2000);
        builder.Property(x => x.TotalNet).HasPrecision(18, 2);
        builder.Property(x => x.TotalTax).HasPrecision(18, 2);
        builder.Property(x => x.TotalGross).HasPrecision(18, 2);

        builder.OwnsOne(x => x.Customer, p => ConfigureParty(p, "Customer"));
        builder.Navigation(x => x.Customer).IsRequired();
        builder.OwnsOne(x => x.Issuer, p => ConfigureParty(p, "Issuer"));

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Lines).AutoInclude();

        // A number is unique within its series; a draft has none. Finbuckle adds the tenant to the index.
        builder.HasIndex(x => new { x.SeriesKey, x.Number }).IsUnique().HasFilter("\"Number\" IS NOT NULL");
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => new { x.Status, x.DueDate });
        builder.HasIndex(x => x.IssueDate);
        builder.HasIndex(x => x.ReferenceDocumentId);
        builder.Ignore(x => x.DomainEvents);
    }

    private static void ConfigureParty(OwnedNavigationBuilder<Document, DocumentParty> party, string prefix)
    {
        party.Property<string>(nameof(DocumentParty.Name)).HasColumnName(prefix + "Name").HasMaxLength(200);
        party.Property<string>(nameof(DocumentParty.AddressLine)).HasColumnName(prefix + "AddressLine").HasMaxLength(200);
        party.Property<string>(nameof(DocumentParty.PostalCode)).HasColumnName(prefix + "PostalCode").HasMaxLength(20);
        party.Property<string>(nameof(DocumentParty.City)).HasColumnName(prefix + "City").HasMaxLength(100);
        party.Property<string>(nameof(DocumentParty.CountryCode)).HasColumnName(prefix + "CountryCode").HasMaxLength(2);
        party.Property<string?>(nameof(DocumentParty.TaxNumber)).HasColumnName(prefix + "TaxNumber").HasMaxLength(32);
    }
}

public sealed class DocumentLineConfiguration : IEntityTypeConfiguration<DocumentLine>
{
    public void Configure(EntityTypeBuilder<DocumentLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("DocumentLines");
        builder.HasKey(x => x.Id);

        // A line is reached only through its document's collection, so EF must insert it, not update it.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Unit).HasMaxLength(20);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 4);
        builder.Property(x => x.TaxCode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.TaxRate).HasPrecision(9, 6);
        builder.Property(x => x.TaxNote).HasMaxLength(500);
        builder.Property(x => x.Net).HasPrecision(18, 2);
        builder.Property(x => x.Tax).HasPrecision(18, 2);
        builder.Property(x => x.Gross).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.DocumentId, x.Position });
        builder.Ignore(x => x.DomainEvents);
    }
}
