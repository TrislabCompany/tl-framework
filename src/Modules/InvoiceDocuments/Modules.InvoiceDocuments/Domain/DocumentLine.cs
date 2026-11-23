using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>One line of a document: what was sold, and the tax the country rule pack decided for it.</summary>
public sealed class DocumentLine : BaseEntity<Guid>
{
    public Guid DocumentId { get; internal set; }
    public int Position { get; private set; }
    public Guid? ItemId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public string? Unit { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string TaxCode { get; private set; } = default!;

    /// <summary>The rate as a fraction: 0.22 is 22 percent.</summary>
    public decimal TaxRate { get; private set; }
    public string? TaxNote { get; private set; }
    public decimal Net { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Gross { get; private set; }

    private DocumentLine() { }

    public static DocumentLine Create(
        Guid? itemId,
        string description,
        decimal quantity,
        string? unit,
        decimal unitPrice,
        string taxCode,
        decimal taxRate,
        string? taxNote)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxCode);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        }
        if (taxRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate), "Tax rate cannot be negative.");
        }

        var net = Round(quantity * unitPrice);
        var tax = Round(net * taxRate);
        return new DocumentLine
        {
            Id = Guid.CreateVersion7(),
            ItemId = itemId,
            Description = description.Trim(),
            Quantity = quantity,
            Unit = unit?.Trim(),
            UnitPrice = unitPrice,
            TaxCode = taxCode.Trim(),
            TaxRate = taxRate,
            TaxNote = taxNote,
            Net = net,
            Tax = tax,
            Gross = net + tax,
        };
    }

    internal void SetPosition(int position) => Position = position;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
