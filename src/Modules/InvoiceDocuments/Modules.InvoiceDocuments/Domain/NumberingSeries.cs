using Trislab.Framework.Core.Domain;

namespace Trislab.Modules.InvoiceDocuments.Domain;

/// <summary>
/// The counter of one numbering series of a tenant, named by the country rule pack. The counter only goes up, in
/// the same transaction that issues the document, so numbers have no gaps and a number is never reused.
/// <c>LastNumber</c> is a concurrency token: two documents issued at once cannot take the same number.
/// </summary>
public sealed class NumberingSeries : AggregateRoot<Guid>
{
    public string SeriesKey { get; private set; } = default!;
    public long LastNumber { get; private set; }

    private NumberingSeries() { }

    public static NumberingSeries Start(string seriesKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesKey);
        return new NumberingSeries { Id = Guid.CreateVersion7(), SeriesKey = seriesKey, LastNumber = 0 };
    }

    /// <summary>Takes the next number of the series.</summary>
    public long Next() => ++LastNumber;
}
