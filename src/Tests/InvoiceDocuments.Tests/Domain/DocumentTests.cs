using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace InvoiceDocuments.Tests.Domain;

public sealed class DocumentTests
{
    private static readonly DocumentParty Customer = new("Buyer", "Street 1", "1000", "Town", "ZZ", "ZZ1");
    private static readonly DocumentParty Issuer = new("Seller", "Street 2", "2000", "City", "ZZ", "ZZ2");

    private static DocumentLine Line(decimal quantity = 1m, decimal price = 100m, decimal rate = 0.2m)
        => DocumentLine.Create(null, "Work", quantity, "h", price, "S", rate, null);

    private static Document Draft(DocumentType type = DocumentType.Invoice, Guid? reference = null, params DocumentLine[] lines)
        => Document.CreateDraft(type, Guid.NewGuid(), Customer, "eur", null, null, reference, lines.Length == 0 ? [Line()] : lines);

    private static Document Issued(DocumentType type = DocumentType.Invoice)
    {
        var document = Draft(type, type == DocumentType.CreditNote ? Guid.NewGuid() : null);
        document.Issue("N-1", "series", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 15), Issuer, null, ["h"], ["f"]);
        return document;
    }

    [Fact]
    public void Line_Should_Round_Net_And_Tax_To_Two_Decimals()
    {
        var line = DocumentLine.Create(null, "Bolt", 3m, null, 0.335m, "S", 0.22m, null);

        line.Net.ShouldBe(1.01m);    // 1.005 rounded away from zero
        line.Tax.ShouldBe(0.22m);    // 1.01 * 0.22 = 0.2222
        line.Gross.ShouldBe(1.23m);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, -1)]
    public void Line_Should_Refuse_A_Bad_Quantity_Or_Price(double quantity, double price)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            DocumentLine.Create(null, "x", (decimal)quantity, null, (decimal)price, "S", 0.2m, null));
    }

    [Fact]
    public void Draft_Should_Total_Its_Lines_And_Number_Them_From_One()
    {
        var document = Draft(DocumentType.Invoice, null, Line(2m, 100m), Line(1m, 50m, 0m));

        document.TotalNet.ShouldBe(250m);
        document.TotalTax.ShouldBe(40m);
        document.TotalGross.ShouldBe(290m);
        document.Lines.Select(l => l.Position).ShouldBe([1, 2]);
        document.Status.ShouldBe(DocumentStatus.Draft);
        document.Number.ShouldBeNull();
        document.Currency.ShouldBe("EUR");
    }

    [Fact]
    public void CreditNote_Should_Need_The_Invoice_It_Corrects()
    {
        Should.Throw<InvalidOperationException>(() => Draft(DocumentType.CreditNote, null));
    }

    [Fact]
    public void Draft_Can_Change_But_An_Issued_Document_Cannot()
    {
        var document = Draft();
        document.UpdateDraft(Guid.NewGuid(), Customer, null, "note", [Line(2m)]);
        document.TotalNet.ShouldBe(200m);

        document.Issue("N-1", "series", new DateOnly(2026, 5, 1), null, Issuer, "ZZ00", [], []);

        document.Status.ShouldBe(DocumentStatus.Issued);
        document.Number.ShouldBe("N-1");
        document.Issuer.ShouldBe(Issuer);
        document.IssuedAtUtc.ShouldNotBeNull();
        Should.Throw<InvalidOperationException>(() => document.UpdateDraft(Guid.NewGuid(), Customer, null, null, [Line()]));
        Should.Throw<InvalidOperationException>(() => document.Reprice(Customer, [Line()]));
        Should.Throw<InvalidOperationException>(() => document.Issue("N-2", "series", new DateOnly(2026, 5, 1), null, Issuer, null, [], []));
    }

    [Fact]
    public void Issue_Should_Store_The_Pack_Text_Lines()
    {
        var document = Issued();

        document.HeaderText.ShouldBe("h");
        document.FooterText.ShouldBe("f");
    }

    [Theory]
    [InlineData(DocumentType.Invoice, true)]
    [InlineData(DocumentType.AdvanceInvoice, true)]
    [InlineData(DocumentType.ProForma, false)]
    [InlineData(DocumentType.Quote, false)]
    [InlineData(DocumentType.CreditNote, false)]
    public void Only_Invoices_And_Advance_Invoices_Are_Paid(DocumentType type, bool payable)
    {
        Document.IsPayable(type).ShouldBe(payable);
        var document = Issued(type);

        if (payable)
        {
            document.MarkPaid(DateTime.UtcNow);
            document.Status.ShouldBe(DocumentStatus.Paid);
        }
        else
        {
            Should.Throw<InvalidOperationException>(() => document.MarkPaid(DateTime.UtcNow));
        }
    }

    [Fact]
    public void A_Draft_Or_Paid_Document_Cannot_Be_Marked_Paid()
    {
        Should.Throw<InvalidOperationException>(() => Draft().MarkPaid(DateTime.UtcNow));
        var document = Issued();
        document.MarkPaid(DateTime.UtcNow);
        Should.Throw<InvalidOperationException>(() => document.MarkPaid(DateTime.UtcNow));
    }

    [Theory]
    [InlineData(DocumentType.Quote, DocumentType.ProForma, true)]
    [InlineData(DocumentType.Quote, DocumentType.Invoice, true)]
    [InlineData(DocumentType.ProForma, DocumentType.Invoice, true)]
    [InlineData(DocumentType.Invoice, DocumentType.CreditNote, false)]
    [InlineData(DocumentType.ProForma, DocumentType.Quote, false)]
    [InlineData(DocumentType.Quote, DocumentType.Quote, false)]
    [InlineData(DocumentType.AdvanceInvoice, DocumentType.Invoice, false)]
    public void Conversion_Matrix(DocumentType from, DocumentType to, bool allowed)
    {
        Document.CanConvert(from, to).ShouldBe(allowed);
    }

    [Fact]
    public void MarkConverted_Should_Need_An_Issued_Document()
    {
        Should.Throw<InvalidOperationException>(() => Draft(DocumentType.ProForma).MarkConverted(Guid.NewGuid()));
        var document = Issued(DocumentType.ProForma);
        var target = Guid.NewGuid();

        document.MarkConverted(target);

        document.Status.ShouldBe(DocumentStatus.Converted);
        document.ConvertedToDocumentId.ShouldBe(target);
    }

    [Fact]
    public void IsOverdue_Should_Be_True_Only_For_An_Open_Invoice_Past_Its_Due_Date()
    {
        var document = Issued();   // due 2026-05-15

        document.IsOverdue(new DateOnly(2026, 5, 15)).ShouldBeFalse();
        document.IsOverdue(new DateOnly(2026, 5, 16)).ShouldBeTrue();
        document.MarkPaid(DateTime.UtcNow);
        document.IsOverdue(new DateOnly(2026, 6, 1)).ShouldBeFalse();
        Issued(DocumentType.ProForma).IsOverdue(new DateOnly(2027, 1, 1)).ShouldBeFalse();
    }

    [Fact]
    public void MarkSent_Should_Not_Change_A_Draft_But_Record_The_Time_On_An_Issued_Document()
    {
        Should.Throw<InvalidOperationException>(() => Draft().MarkSent(DateTime.UtcNow));
        var document = Issued();

        document.MarkSent(new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc));

        document.SentAtUtc.ShouldNotBeNull();
        document.Status.ShouldBe(DocumentStatus.Issued);
    }
}
