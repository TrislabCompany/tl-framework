using Trislab.Modules.InvoiceDocuments.Contracts;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Customers;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Documents;
using Trislab.Modules.InvoiceDocuments.Features.v1.Customers.CreateCustomer;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.CreateDocumentDraft;
using Trislab.Modules.InvoiceDocuments.Features.v1.Documents.GetDocuments;

namespace InvoiceDocuments.Tests.Validators;

public sealed class InvoiceDocumentsValidatorsTests
{
    private static DocumentLineInput ValidLine() => new(null, "Work", 1m, "h", 10m, "S");

    [Fact]
    public void Draft_Should_Pass_With_A_Valid_Line()
    {
        var command = new CreateDocumentDraftCommand(DocumentType.Invoice, Guid.NewGuid(), null, null, null, null, [ValidLine()]);

        new CreateDocumentDraftCommandValidator().Validate(command).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Draft_Should_Fail_Without_Lines_Or_Customer()
    {
        var command = new CreateDocumentDraftCommand(DocumentType.Invoice, Guid.Empty, null, null, null, null, []);

        var result = new CreateDocumentDraftCommandValidator().Validate(command);

        result.Errors.Select(e => e.PropertyName).ShouldContain("CustomerId");
        result.Errors.Select(e => e.PropertyName).ShouldContain("Lines");
    }

    [Fact]
    public void Draft_Should_Fail_For_A_Credit_Note_Without_A_Reference()
    {
        var command = new CreateDocumentDraftCommand(DocumentType.CreditNote, Guid.NewGuid(), null, null, null, null, [ValidLine()]);

        new CreateDocumentDraftCommandValidator().Validate(command).Errors
            .ShouldContain(e => e.PropertyName == "ReferenceDocumentId");
    }

    [Fact]
    public void Line_Should_Need_An_Item_Or_All_Of_Description_Price_And_Tax_Code()
    {
        var validator = new DocumentLineInputValidator();

        validator.Validate(new DocumentLineInput(Guid.NewGuid(), null, 1m, null, null, null)).IsValid.ShouldBeTrue();
        validator.Validate(new DocumentLineInput(null, "Work", 1m, null, 10m, null)).IsValid.ShouldBeFalse();
        validator.Validate(new DocumentLineInput(null, "Work", 0m, null, 10m, "S")).IsValid.ShouldBeFalse();
        validator.Validate(new DocumentLineInput(null, "Work", 1m, null, -1m, "S")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Customer_Should_Need_A_Two_Letter_Country_And_A_Valid_Email()
    {
        var validator = new CreateCustomerCommandValidator();
        var good = new CreateCustomerCommand("Buyer", "a@b.test", "Street", "1000", "Town", "SI", null, null);

        validator.Validate(good).IsValid.ShouldBeTrue();
        validator.Validate(good with { CountryCode = "SVN" }).IsValid.ShouldBeFalse();
        validator.Validate(good with { Email = "not-an-email" }).IsValid.ShouldBeFalse();
        validator.Validate(good with { Name = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void List_Should_Limit_The_Page_Size()
    {
        var validator = new GetDocumentsQueryValidator();

        validator.Validate(new GetDocumentsQuery()).IsValid.ShouldBeTrue();
        validator.Validate(new GetDocumentsQuery(PageSize: 101)).IsValid.ShouldBeFalse();
        validator.Validate(new GetDocumentsQuery(PageNumber: 0)).IsValid.ShouldBeFalse();
    }
}
