using System.Globalization;
using System.IO.Compression;
using System.Text;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace Trislab.Modules.InvoiceDocuments.Services;

/// <summary>Builds the accountant's export: <c>documents.csv</c> and one PDF per document, in one zip.</summary>
internal static class DocumentExportBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static byte[] Build(IReadOnlyList<Document> documents, IDocumentPdfRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(renderer);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var csv = zip.CreateEntry("documents.csv");
            using (var writer = new StreamWriter(csv.Open(), new UTF8Encoding(false)))
            {
                writer.Write(Csv(documents));
            }

            foreach (var document in documents)
            {
                var entry = zip.CreateEntry($"pdf/{FileSafe(document.Number!)}.pdf");
                using var entryStream = entry.Open();
                var pdf = renderer.Render(document.ToDto());
                entryStream.Write(pdf, 0, pdf.Length);
            }
        }

        return stream.ToArray();
    }

    public static string Csv(IReadOnlyList<Document> documents)
    {
        var sb = new StringBuilder();
        sb.Append("Number,Type,Status,IssueDate,DueDate,PaidAt,CustomerName,CustomerTaxNumber,Currency,Net,Tax,Gross\r\n");
        foreach (var d in documents)
        {
            sb.Append(string.Join(',',
                Field(d.Number),
                Field(d.Type.ToString()),
                Field(d.Status.ToString()),
                Field(d.IssueDate?.ToString("yyyy-MM-dd", Culture)),
                Field(d.DueDate?.ToString("yyyy-MM-dd", Culture)),
                Field(d.PaidAtUtc?.ToString("yyyy-MM-dd", Culture)),
                Field(d.Customer.Name),
                Field(d.Customer.TaxNumber),
                Field(d.Currency),
                d.TotalNet.ToString("0.00", Culture),
                d.TotalTax.ToString("0.00", Culture),
                d.TotalGross.ToString("0.00", Culture)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // A cell that starts with =, +, - or @ would be run as a formula by a spreadsheet.
        if (value[0] is '=' or '+' or '-' or '@')
        {
            value = "'" + value;
        }

        return value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    public static string FileSafe(string number)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = number.Select(c => invalid.Contains(c) || c is '/' or '\\' ? '-' : c).ToArray();
        return new string(chars);
    }
}
