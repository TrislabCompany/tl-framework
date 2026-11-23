using Trislab.Modules.InvoiceDocuments.Domain;

namespace InvoiceDocuments.Tests.Domain;

public sealed class CustomerItemSeriesTests
{
    [Fact]
    public void Customer_Should_Trim_And_Upper_Case_The_Country()
    {
        var customer = Customer.Create(" Buyer ", " a@b.test ", " Street 1 ", "1000", "Town", "si", " 123 ", null);

        customer.Name.ShouldBe("Buyer");
        customer.Email.ShouldBe("a@b.test");
        customer.CountryCode.ShouldBe("SI");
        customer.TaxNumber.ShouldBe("123");
        customer.ToParty().Name.ShouldBe("Buyer");
    }

    [Fact]
    public void Customer_Delete_Should_Be_Soft_And_Idempotent()
    {
        var customer = Customer.Create("Buyer", null, "S", "1", "T", "SI", null, null);

        customer.Delete("user");
        var deletedOn = customer.DeletedOnUtc;
        customer.Delete("other");

        customer.IsDeleted.ShouldBeTrue();
        customer.DeletedBy.ShouldBe("user");
        customer.DeletedOnUtc.ShouldBe(deletedOn);
    }

    [Fact]
    public void Customer_Should_Need_A_Name()
    {
        Should.Throw<ArgumentException>(() => Customer.Create(" ", null, "S", "1", "T", "SI", null, null));
    }

    [Fact]
    public void Item_Should_Refuse_A_Negative_Price_Or_A_Missing_Tax_Code()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Item.Create("Work", null, "h", -1m, "S"));
        Should.Throw<ArgumentException>(() => Item.Create("Work", null, "h", 1m, " "));
        Item.Create("Work", null, "h", 1m, "S").TaxCode.ShouldBe("S");
    }

    [Fact]
    public void IssuerProfile_Should_Normalise_The_Iban_And_Currency()
    {
        var profile = IssuerProfile.Create("Seller", "S", "1", "T", "si", "123", "si56 1234 5678", null, "eur", 14);

        profile.Iban.ShouldBe("SI561234 5678".Replace(" ", string.Empty, StringComparison.Ordinal));
        profile.DefaultCurrency.ShouldBe("EUR");
        profile.CountryCode.ShouldBe("SI");
    }

    [Fact]
    public void NumberingSeries_Should_Count_Up_From_One_Without_Gaps()
    {
        var series = NumberingSeries.Start("invoice-2026");

        series.Next().ShouldBe(1);
        series.Next().ShouldBe(2);
        series.Next().ShouldBe(3);
        series.LastNumber.ShouldBe(3);
    }
}
