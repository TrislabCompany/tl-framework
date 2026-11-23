using System.IO.Compression;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Domain;
using Trislab.Modules.InvoiceDocuments.Services;

namespace InvoiceDocuments.Tests.Services;

public sealed class ExportAndPdfTests
{
    private static Document IssuedDocument(string customerName, string number)
    {
        var customer = new DocumentParty(customerName, "Street 1", "1000", "Town", "ZZ", "ZZ1");
        var line = DocumentLine.Create(null, "Work", 1m, "h", 100m, "S", 0.2m, "A note");
        var document = Document.CreateDraft(DocumentType.Invoice, Guid.NewGuid(), customer, "EUR", null, "Thanks", null, [line]);
        document.Issue(number, "invoice-2026", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 15),
            new DocumentParty("Seller", "Street 2", "2000", "City", "ZZ", "ZZ2"), "ZZ00TEST", ["Header text"], ["Footer text"]);
        return document;
    }

    [Fact]
    public void Csv_Should_Quote_Fields_With_Commas_Or_Quotes()
    {
        var csv = DocumentExportBuilder.Csv([IssuedDocument("Smith, \"Jones\" & Co", "INV-2026-0001")]);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldStartWith("Number,Type,Status");
        lines[1].ShouldContain("INV-2026-0001,Invoice,Issued,2026-05-01,2026-05-15,,\"Smith, \"\"Jones\"\" & Co\"");
        lines[1].ShouldEndWith("EUR,100.00,20.00,120.00");
    }

    [Fact]
    public void Csv_Should_Neutralise_A_Formula_In_A_Text_Field()
    {
        var csv = DocumentExportBuilder.Csv([IssuedDocument("=HYPERLINK(\"x\")", "INV-1")]);

        csv.ShouldNotContain(",=HYPERLINK");
        csv.ShouldContain("'=HYPERLINK");
    }

    [Fact]
    public void FileSafe_Should_Replace_Path_Characters()
    {
        DocumentExportBuilder.FileSafe("INV/2026\\0001").ShouldBe("INV-2026-0001");
    }

    [Fact]
    public void Pdf_Should_Render_A_Document()
    {
        var pdf = new DocumentPdfRenderer().Render(IssuedDocument("Buyer", "INV-2026-0001").ToDto());

        System.Text.Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
        pdf.Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public void Export_Should_Hold_The_Csv_And_One_Pdf_Per_Document()
    {
        var zipBytes = DocumentExportBuilder.Build(
            [IssuedDocument("A", "INV-2026-0001"), IssuedDocument("B", "INV-2026-0002")], new DocumentPdfRenderer());

        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        zip.Entries.Select(e => e.FullName).ShouldBe(
            ["documents.csv", "pdf/INV-2026-0001.pdf", "pdf/INV-2026-0002.pdf"], ignoreOrder: true);
    }
}
