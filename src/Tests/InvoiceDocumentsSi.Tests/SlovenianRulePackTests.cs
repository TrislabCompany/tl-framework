using Microsoft.Extensions.DependencyInjection;
using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.RulePack;
using Trislab.Modules.InvoiceDocuments.Si;

namespace InvoiceDocumentsSi.Tests;

public sealed class SlovenianRulePackTests
{
    // 12345679 is a valid tax number (check digit 9), 12345678 is not.
    private const string ValidTaxNumber = "12345679";
    private static readonly DateOnly Today = new(2026, 5, 4);
    private static readonly SlovenianRulePack Pack = new();

    private static PartyForRules Issuer(string? taxNumber = "SI" + ValidTaxNumber)
        => new("Seller d.o.o.", "Ulica 1", "1000", "Ljubljana", "SI", taxNumber);

    private static PartyForRules Customer(string? taxNumber = "SI" + ValidTaxNumber, string country = "SI")
        => new("Buyer d.o.o.", "Cesta 2", "2000", "Maribor", country, taxNumber);

    private static TaxRequest Tax(string code, PartyForRules? issuer = null, DateOnly? date = null)
        => new(DocumentType.Invoice, date ?? Today, code, issuer ?? Issuer(), Customer());

    private static LineForRules Line(string code = "S", decimal net = 100m, decimal rate = 0.22m)
        => new("Work", 1m, "h", net, code, rate, net, Math.Round(net * rate, 2));

    private static DocumentForRules Doc(
        DocumentType type = DocumentType.Invoice, PartyForRules? issuer = null, PartyForRules? customer = null,
        string? reference = null, params LineForRules[] lines)
        => new(type, Today, Today.AddDays(14), "EUR", issuer ?? Issuer(), "SI56000000000000000", customer ?? Customer(),
            reference, lines.Length == 0 ? [Line()] : lines);

    // ─── VAT ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("S", "0.22")]
    [InlineData("R1", "0.095")]
    [InlineData("R2", "0.05")]
    public void Rated_Codes_Should_Give_Their_Rate_And_No_Note(string code, string rate)
    {
        var decision = Pack.DecideTax(Tax(code));

        // A string, not a double: (decimal)0.22 is not exactly 0.22m since .NET 11.
        decision.Rate.ShouldBe(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture));
        decision.Note.ShouldBeNull();
    }

    [Fact]
    public void Rate_Should_Depend_On_The_Date()
    {
        SiVat.RateOf("S", new DateOnly(2013, 6, 30)).ShouldBeNull();
        SiVat.RateOf("S", new DateOnly(2013, 7, 1)).ShouldBe(0.22m);
        SiVat.RateOf("S", new DateOnly(2040, 1, 1)).ShouldBe(0.22m);
        SiVat.RateOf("Z", Today).ShouldBeNull();
    }

    [Fact]
    public void Zero_Rate_Should_Say_That_No_Vat_Is_Charged()
    {
        var decision = Pack.DecideTax(Tax("Z"));

        decision.Rate.ShouldBe(0m);
        decision.Note.ShouldBe(SiVat.ZeroRateNote);
    }

    [Fact]
    public void Reverse_Charge_Should_Be_Zero_With_The_Required_Note()
    {
        var decision = Pack.DecideTax(Tax("RC"));

        decision.Rate.ShouldBe(0m);
        decision.Note!.ShouldContain("Obrnjena davčna obveznost");
    }

    [Fact]
    public void Exemption_Should_Carry_Its_Legal_Reference_In_The_Note()
    {
        var decision = Pack.DecideTax(Tax("E:42. člen ZDDV-1"));

        decision.Rate.ShouldBe(0m);
        decision.Note.ShouldBe("Oprostitev DDV: 42. člen ZDDV-1.");
    }

    [Fact]
    public void An_Issuer_Without_Vat_Registration_Should_Charge_No_Vat_Whatever_The_Code()
    {
        var decision = Pack.DecideTax(Tax("S", Issuer(ValidTaxNumber)));

        decision.Rate.ShouldBe(0m);
        decision.Note.ShouldBe(SiVat.NotVatRegisteredNote);
        SlovenianRulePack.IsVatRegistered(Issuer(ValidTaxNumber)).ShouldBeFalse();
        SlovenianRulePack.IsVatRegistered(Issuer()).ShouldBeTrue();
    }

    [Fact]
    public void An_Unknown_Code_Should_Charge_Nothing_And_Fail_Validation()
    {
        Pack.DecideTax(Tax("XX")).Rate.ShouldBe(0m);

        Pack.Validate(Doc(lines: Line("XX", 10m, 0m))).ShouldContain(v => v.Code == "si-tax-code");
    }

    // ─── tax number ──────────────────────────────────────────────────

    [Theory]
    [InlineData("12345679", true, false)]
    [InlineData("SI12345679", true, true)]
    [InlineData("si 1234 5679", true, true)]
    [InlineData("12345678", false, false)]
    [InlineData("1234567", false, false)]
    [InlineData("AB345679", false, false)]
    [InlineData("", false, false)]
    public void Tax_Number_Should_Be_Checked(string value, bool valid, bool vatPrefix)
    {
        SiTaxNumber.TryParse(value, out _, out var prefix).ShouldBe(valid);
        if (valid)
        {
            prefix.ShouldBe(vatPrefix);
        }
    }

    // ─── required content ────────────────────────────────────────────

    [Fact]
    public void A_Complete_Invoice_Should_Pass()
    {
        Pack.Validate(Doc()).ShouldBeEmpty();
    }

    [Fact]
    public void An_Invoice_Should_Need_The_Issuers_Valid_Tax_Number_And_The_Parties_Addresses()
    {
        var bad = Doc(
            issuer: Issuer("12345678") with { AddressLine = "" },
            customer: Customer() with { AddressLine = "", Name = "" });

        var codes = Pack.Validate(bad).Select(v => v.Code).ToList();

        codes.ShouldContain("si-issuer-tax-number");
        codes.ShouldContain("si-issuer-address");
        codes.ShouldContain("si-customer-address");
        codes.ShouldContain("si-customer-name");
    }

    [Fact]
    public void A_Slovenian_Customers_Bad_Tax_Number_Should_Fail_But_A_Foreign_One_Is_Not_Checked()
    {
        Pack.Validate(Doc(customer: Customer("12345678"))).ShouldContain(v => v.Code == "si-customer-tax-number");
        Pack.Validate(Doc(customer: Customer("DE123456789", "DE"))).ShouldBeEmpty();
        Pack.Validate(Doc(customer: Customer(null))).ShouldBeEmpty();
    }

    [Fact]
    public void Reverse_Charge_Should_Need_The_Customers_Vat_Number()
    {
        var line = Line("RC", 100m, 0m);

        Pack.Validate(Doc(customer: Customer(null), lines: line)).ShouldContain(v => v.Code == "si-reverse-charge-customer");
        Pack.Validate(Doc(lines: line)).ShouldBeEmpty();
    }

    [Fact]
    public void A_Credit_Note_Should_Need_The_Number_Of_The_Invoice()
    {
        Pack.Validate(Doc(DocumentType.CreditNote)).ShouldContain(v => v.Code == "si-credit-note-reference");
        Pack.Validate(Doc(DocumentType.CreditNote, reference: "RAC-2026-00001")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(DocumentType.Quote)]
    [InlineData(DocumentType.ProForma)]
    public void A_Quote_Or_Pro_Forma_Should_Not_Need_Tax_Numbers(DocumentType type)
    {
        var doc = Doc(type, issuer: Issuer(null), customer: Customer(null) with { AddressLine = "" });

        Pack.Validate(doc).ShouldBeEmpty();
    }

    [Fact]
    public void A_Document_Before_The_Supported_Date_Should_Fail()
    {
        var doc = Doc() with { IssueDate = new DateOnly(2013, 6, 30) };

        Pack.Validate(doc).ShouldContain(v => v.Code == "si-date");
    }

    // ─── numbering, due date, retention ──────────────────────────────

    [Theory]
    [InlineData(DocumentType.Quote, "PON")]
    [InlineData(DocumentType.ProForma, "PRE")]
    [InlineData(DocumentType.Invoice, "RAC")]
    [InlineData(DocumentType.CreditNote, "DOB")]
    [InlineData(DocumentType.AdvanceInvoice, "AVA")]
    public void Each_Type_Should_Have_Its_Own_Series_Per_Year(DocumentType type, string prefix)
    {
        Pack.SeriesKey(type, Today).ShouldBe($"{prefix}-2026");
        Pack.SeriesKey(type, new DateOnly(2027, 1, 1)).ShouldBe($"{prefix}-2027");
        Pack.FormatNumber(type, Today, 7).ShouldBe($"{prefix}-2026-00007");
    }

    [Fact]
    public void A_Number_Should_Be_Unique_Across_Types_And_Years()
    {
        var numbers = new[]
        {
            Pack.FormatNumber(DocumentType.Invoice, Today, 1),
            Pack.FormatNumber(DocumentType.CreditNote, Today, 1),
            Pack.FormatNumber(DocumentType.Invoice, new DateOnly(2027, 1, 1), 1),
        };

        numbers.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Due_Date_Should_Follow_The_Payment_Term_Except_For_A_Quote()
    {
        Pack.DefaultDueDate(DocumentType.Invoice, Today, 14).ShouldBe(Today.AddDays(14));
        Pack.DefaultDueDate(DocumentType.Quote, Today, 14).ShouldBeNull();
    }

    [Fact]
    public void Retention_Should_Be_Ten_Years_And_Twenty_For_Real_Estate()
    {
        Pack.RetentionYears.ShouldBe(10);
        SlovenianRulePack.RetentionYearsRealEstate.ShouldBe(20);
    }

    // ─── printed text ────────────────────────────────────────────────

    [Fact]
    public void Layout_Should_Show_The_Taxable_Amount_And_Vat_For_Each_Rate()
    {
        var doc = Doc(DocumentType.Invoice, null, null, null, Line("S", 100m, 0.22m), Line("R1", 200m, 0.095m));

        var footer = Pack.GetLayoutAdditions(doc).FooterLines;

        footer.ShouldContain("DDV 22 %: osnova 100.00 EUR, DDV 22.00 EUR");
        footer.ShouldContain("DDV 9.5 %: osnova 200.00 EUR, DDV 19.00 EUR");
    }

    [Fact]
    public void Layout_Should_Print_Each_Legal_Note_Once()
    {
        var doc = Doc(DocumentType.Invoice, null, null, null, Line("RC", 10m, 0m), Line("RC", 20m, 0m), Line("E:42. člen ZDDV-1", 5m, 0m));

        var footer = Pack.GetLayoutAdditions(doc).FooterLines;

        footer.Count(l => l == SiVat.ReverseChargeNote).ShouldBe(1);
        footer.ShouldContain("Oprostitev DDV: 42. člen ZDDV-1.");
    }

    [Fact]
    public void Layout_Should_Print_The_Non_Vat_Note_For_An_Unregistered_Issuer()
    {
        Pack.GetLayoutAdditions(Doc(issuer: Issuer(ValidTaxNumber))).FooterLines.ShouldContain(SiVat.NotVatRegisteredNote);
    }

    [Fact]
    public void Layout_Should_Refer_A_Credit_Note_To_Its_Invoice_And_Mark_A_Pro_Forma()
    {
        Pack.GetLayoutAdditions(Doc(DocumentType.CreditNote, reference: "RAC-2026-00001")).HeaderLines
            .ShouldContain("Dobropis k računu št. RAC-2026-00001");
        Pack.GetLayoutAdditions(Doc(DocumentType.ProForma)).FooterLines
            .ShouldContain(l => l.StartsWith("Predračun ni račun", StringComparison.Ordinal));
        Pack.GetLayoutAdditions(Doc(DocumentType.Quote)).FooterLines.ShouldBeEmpty();
    }

    // ─── installing ──────────────────────────────────────────────────

    [Fact]
    public void AddInvoiceDocumentsSi_Should_Register_The_Pack_Once()
    {
        var services = new ServiceCollection();

        services.AddInvoiceDocumentsSi().AddInvoiceDocumentsSi();

        using var provider = services.BuildServiceProvider();
        var packs = provider.GetServices<IInvoiceDocumentsRulePack>().ToList();
        packs.Count.ShouldBe(1);
        packs[0].CountryCode.ShouldBe("SI");
    }
}
