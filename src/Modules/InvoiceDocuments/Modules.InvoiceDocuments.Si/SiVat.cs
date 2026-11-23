namespace Trislab.Modules.InvoiceDocuments.Si;

/// <summary>
/// The tax codes of the Slovenian pack and what they mean. A tax code is what an item or a document line carries;
/// the pack turns it into a rate and a note on the day of the document. The rates and their dates are data in
/// this one file, so a change in the law is a change here (and an accountant reviews it).
/// </summary>
public static class SiVat
{
    /// <summary>The standard rate.</summary>
    public const string Standard = "S";

    /// <summary>The reduced rate of 9.5 percent.</summary>
    public const string Reduced = "R1";

    /// <summary>The lower reduced rate of 5 percent.</summary>
    public const string LowReduced = "R2";

    /// <summary>Zero rate, for example an export or an intra-Community supply: no VAT is charged, the note says why.</summary>
    public const string Zero = "Z";

    /// <summary>Reverse charge: the recipient accounts for the VAT. The customer's VAT number is required.</summary>
    public const string ReverseCharge = "RC";

    /// <summary>Prefix of an exemption: <c>E:</c> followed by the legal reference, for example <c>E:42. člen ZDDV-1</c>.</summary>
    public const string ExemptPrefix = "E:";

    /// <summary>The note of an issuer who is not registered for VAT (the small-taxpayer exemption).</summary>
    public const string NotVatRegisteredNote = "DDV ni obračunan na podlagi 1. odstavka 94. člena ZDDV-1.";

    public const string ReverseChargeNote = "Obrnjena davčna obveznost: DDV obračuna prejemnik (76.a člen ZDDV-1).";

    public const string ZeroRateNote = "DDV ni obračunan (0 %).";

    /// <summary>The first day the rates below apply; earlier documents are refused.</summary>
    public static readonly DateOnly FirstSupportedDate = new(2013, 7, 1);

    private static readonly (string Code, decimal Rate, DateOnly From)[] Rates =
    [
        (Standard, 0.22m, new DateOnly(2013, 7, 1)),
        (Reduced, 0.095m, new DateOnly(2013, 7, 1)),
        (LowReduced, 0.05m, new DateOnly(2013, 7, 1)),
    ];

    /// <summary>The rate of a rated code on a date, or null when the code has no rate then.</summary>
    public static decimal? RateOf(string code, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(code);
        decimal? found = null;
        DateOnly latest = DateOnly.MinValue;
        foreach (var (rateCode, rate, from) in Rates)
        {
            if (string.Equals(rateCode, code, StringComparison.Ordinal) && from <= date && from >= latest)
            {
                found = rate;
                latest = from;
            }
        }

        return found;
    }

    public static bool IsExempt(string code) => code is not null && code.StartsWith(ExemptPrefix, StringComparison.Ordinal) && code.Length > ExemptPrefix.Length;

    /// <summary>Whether the code is one the pack knows.</summary>
    public static bool IsKnown(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        return Rates.Any(r => string.Equals(r.Code, code, StringComparison.Ordinal))
            || code is Zero or ReverseCharge
            || IsExempt(code);
    }

    /// <summary>The legal reference of an exemption code (the part after <c>E:</c>).</summary>
    public static string ExemptionReason(string code) => code[ExemptPrefix.Length..].Trim();
}
