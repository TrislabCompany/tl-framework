using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.RulePack;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace Trislab.Modules.InvoiceDocuments.Services;

/// <summary>Finds the country rule pack of an issuer's country among the packs the product installed.</summary>
public sealed class RulePackResolver(IEnumerable<IInvoiceDocumentsRulePack> packs)
{
    private readonly Dictionary<string, IInvoiceDocumentsRulePack> _packs =
        packs.ToDictionary(p => p.CountryCode.ToUpperInvariant(), StringComparer.Ordinal);

    public IInvoiceDocumentsRulePack Get(string countryCode)
    {
        ArgumentNullException.ThrowIfNull(countryCode);
        return _packs.TryGetValue(countryCode.ToUpperInvariant(), out var pack)
            ? pack
            : throw DocumentErrors.RulePackMissing(countryCode);
    }
}

/// <summary>Loads the tenant's issuer profile and turns document inputs into priced lines.</summary>
public sealed class DocumentSupport(InvoiceDocumentsDbContext dbContext, RulePackResolver resolver)
{
    public async Task<IssuerProfile> GetIssuerProfileAsync(CancellationToken cancellationToken)
        => await dbContext.IssuerProfiles.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false)
           ?? throw DocumentErrors.IssuerProfileRequired();

    public IInvoiceDocumentsRulePack GetPack(IssuerProfile issuer)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        return resolver.Get(issuer.CountryCode);
    }

    public async Task<Customer> GetCustomerAsync(Guid customerId, CancellationToken cancellationToken)
        => await dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken).ConfigureAwait(false)
           ?? throw DocumentErrors.CustomerNotFound(customerId);

    /// <summary>
    /// Builds the lines of a document. A field left empty on an input comes from its item; the tax of every
    /// line is decided by the country rule pack.
    /// </summary>
    public async Task<List<DocumentLine>> BuildLinesAsync(
        IReadOnlyList<DocumentLineInput> inputs,
        DocumentType type,
        DateOnly date,
        DocumentParty issuer,
        DocumentParty customer,
        IInvoiceDocumentsRulePack pack,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(pack);
        var itemIds = inputs.Where(i => i.ItemId.HasValue).Select(i => i.ItemId!.Value).Distinct().ToList();
        var items = itemIds.Count == 0
            ? []
            : await dbContext.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, cancellationToken).ConfigureAwait(false);

        var issuerRules = ToRules(issuer);
        var customerRules = ToRules(customer);
        var lines = new List<DocumentLine>(inputs.Count);
        for (int index = 0; index < inputs.Count; index++)
        {
            var input = inputs[index];
            Item? item = null;
            if (input.ItemId is { } itemId && !items.TryGetValue(itemId, out item))
            {
                throw DocumentErrors.ItemNotFound(itemId);
            }

            var description = FirstNonBlank(input.Description, item?.Name);
            var taxCode = FirstNonBlank(input.TaxCode, item?.TaxCode);
            var unitPrice = input.UnitPrice ?? item?.UnitPrice;
            if (description is null || taxCode is null || unitPrice is null)
            {
                throw DocumentErrors.LineIncomplete(index + 1);
            }

            var decision = pack.DecideTax(new TaxRequest(type, date, taxCode, issuerRules, customerRules));
            lines.Add(DocumentLine.Create(
                input.ItemId, description, input.Quantity, input.Unit ?? item?.Unit, unitPrice.Value,
                taxCode, decision.Rate, decision.Note));
        }

        return lines;
    }

    public static PartyForRules ToRules(DocumentParty party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return new(party.Name, party.AddressLine, party.PostalCode, party.City, party.CountryCode, party.TaxNumber);
    }

    public static DocumentForRules ToRules(Document document, DateOnly issueDate, DocumentParty issuer, string? issuerIban, string? referenceNumber)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(
            document.Type,
            issueDate,
            document.DueDate,
            document.Currency,
            ToRules(issuer),
            issuerIban,
            ToRules(document.Customer),
            referenceNumber,
            document.Lines
                .Select(l => new LineForRules(l.Description, l.Quantity, l.Unit, l.UnitPrice, l.TaxCode, l.TaxRate, l.Net, l.Tax))
                .ToList());
    }

    private static string? FirstNonBlank(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }
}

internal static class DocumentMappings
{
    public static DocumentPartyDto ToDto(this DocumentParty party)
        => new(party.Name, party.AddressLine, party.PostalCode, party.City, party.CountryCode, party.TaxNumber);

    public static DocumentDto ToDto(this Document d) => new(
        d.Id,
        d.Type,
        d.Status,
        d.Number,
        d.CustomerId,
        d.Customer.ToDto(),
        d.Issuer?.ToDto(),
        d.IssuerIban,
        d.Currency,
        d.IssueDate,
        d.DueDate,
        d.PaidAtUtc,
        d.SentAtUtc,
        d.Notes,
        d.ReferenceDocumentId,
        d.ConvertedToDocumentId,
        d.TotalNet,
        d.TotalTax,
        d.TotalGross,
        SplitLines(d.HeaderText),
        SplitLines(d.FooterText),
        d.Lines.OrderBy(l => l.Position)
            .Select(l => new DocumentLineDto(l.Position, l.ItemId, l.Description, l.Quantity, l.Unit, l.UnitPrice,
                l.TaxCode, l.TaxRate, l.TaxNote, l.Net, l.Tax, l.Gross))
            .ToList(),
        d.CreatedAtUtc);

    public static CustomerDto ToDto(this Customer c)
        => new(c.Id, c.Name, c.Email, c.AddressLine, c.PostalCode, c.City, c.CountryCode, c.TaxNumber, c.Note);

    public static ItemDto ToDto(this Item i) => new(i.Id, i.Name, i.Description, i.Unit, i.UnitPrice, i.TaxCode);

    public static IssuerProfileDto ToDto(this IssuerProfile p) => new(
        p.Name, p.AddressLine, p.PostalCode, p.City, p.CountryCode, p.TaxNumber, p.Iban, p.Email,
        p.DefaultCurrency, p.DefaultPaymentTermDays);

    public static DocumentSummaryDto ToSummary(this Document d, DateOnly today) => new(
        d.Id, d.Type, d.Status, d.Number, d.CustomerId, d.Customer.Name, d.Currency,
        d.IssueDate, d.DueDate, d.PaidAtUtc, d.TotalGross, d.IsOverdue(today));

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static List<string> SplitLines(string? text)
        => string.IsNullOrEmpty(text) ? [] : [.. text.Split('\n')];
}
