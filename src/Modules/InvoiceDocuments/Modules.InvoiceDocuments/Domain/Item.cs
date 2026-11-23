using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>A product or service the tenant sells, to pick when writing a document line.</summary>
public sealed class Item : AggregateRoot<Guid>, ISoftDeletable
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? Unit { get; private set; }
    public decimal UnitPrice { get; private set; }

    /// <summary>A code the country rule pack turns into a tax rate; the core does not interpret it.</summary>
    public string TaxCode { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOnUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    private Item() { }

    public static Item Create(string name, string? description, string? unit, decimal unitPrice, string taxCode)
    {
        var item = new Item { Id = Guid.CreateVersion7(), CreatedAtUtc = DateTime.UtcNow };
        item.Apply(name, description, unit, unitPrice, taxCode);
        return item;
    }

    public void Update(string name, string? description, string? unit, decimal unitPrice, string taxCode)
    {
        Apply(name, description, unit, unitPrice, taxCode);
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

    private void Apply(string name, string? description, string? unit, decimal unitPrice, string taxCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxCode);
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }

        Name = name.Trim();
        Description = description?.Trim();
        Unit = unit?.Trim();
        UnitPrice = unitPrice;
        TaxCode = taxCode.Trim();
    }
}
