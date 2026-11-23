namespace Trislab.Modules.InvoiceDocuments.Si;

/// <summary>The Slovenian tax number: eight digits, the last a check digit (modulus 11). The VAT number is <c>SI</c> and the tax number.</summary>
public static class SiTaxNumber
{
    private static readonly int[] Weights = [8, 7, 6, 5, 4, 3, 2];

    /// <summary>Splits <c>SI12345679</c> or <c>12345679</c> into the digits and whether the <c>SI</c> prefix (a VAT number) was there.</summary>
    public static bool TryParse(string? value, out string digits, out bool hasVatPrefix)
    {
        digits = string.Empty;
        hasVatPrefix = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (text.StartsWith("SI", StringComparison.Ordinal))
        {
            hasVatPrefix = true;
            text = text[2..];
        }

        if (text.Length != 8 || !text.All(char.IsAsciiDigit))
        {
            return false;
        }

        digits = text;
        return HasValidCheckDigit(text);
    }

    public static bool HasValidCheckDigit(string eightDigits)
    {
        ArgumentNullException.ThrowIfNull(eightDigits);
        if (eightDigits.Length != 8 || !eightDigits.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        for (int i = 0; i < Weights.Length; i++)
        {
            sum += (eightDigits[i] - '0') * Weights[i];
        }

        int control = 11 - (sum % 11);
        if (control == 11)
        {
            return false;
        }

        if (control == 10)
        {
            control = 0;
        }

        return control == eightDigits[7] - '0';
    }
}
