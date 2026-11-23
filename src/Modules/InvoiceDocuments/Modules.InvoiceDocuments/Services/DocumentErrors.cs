using System.Net;
using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.InvoiceDocuments.Localization;

namespace Trislab.Modules.InvoiceDocuments.Services;

/// <summary>
/// The errors of the module, one factory per case so each resource key is a literal the
/// localization tests can check.
/// </summary>
internal static class DocumentErrors
{
    public static NotFoundException CustomerNotFound(Guid id)
        => (NotFoundException)new NotFoundException($"Customer {id} not found.")
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.CustomerNotFound", id);

    public static NotFoundException ItemNotFound(Guid id)
        => (NotFoundException)new NotFoundException($"Item {id} not found.")
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.ItemNotFound", id);

    public static NotFoundException DocumentNotFound(Guid id)
        => (NotFoundException)new NotFoundException($"Document {id} not found.")
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.DocumentNotFound", id);

    public static CustomException IssuerProfileRequired()
        => new CustomException("Save the business details before creating a document.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.IssuerProfileRequired");

    public static CustomException RulePackMissing(string countryCode)
        => new CustomException($"No country rule pack is installed for country {countryCode}.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.RulePackMissing", countryCode);

    public static CustomException DocumentNotDraft()
        => new CustomException("Only a draft can be changed or deleted.", (IEnumerable<string>?)null, HttpStatusCode.Conflict)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.DocumentNotDraft");

    public static CustomException LineIncomplete(int position)
        => new CustomException($"Line {position} needs a description, a unit price and a tax code, or an item that has them.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.LineIncomplete", position);

    public static CustomException RuleViolations(IEnumerable<string> messages)
    {
        var joined = string.Join(" ", messages);
        return new CustomException($"The document does not meet the rules of the country: {joined}", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.RuleViolations", joined);
    }

    public static CustomException CreditNoteReferenceInvalid()
        => new CustomException("A credit note must refer to an issued or paid invoice.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.CreditNoteReferenceInvalid");

    public static CustomException CreditExceedsInvoice()
        => new CustomException("The credit notes would exceed the total of the invoice.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.CreditExceedsInvoice");

    public static CustomException ConversionNotAllowed(string from, string to)
        => new CustomException($"A {from} cannot be converted into a {to}.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.ConversionNotAllowed", from, to);

    public static CustomException NotPayable()
        => new CustomException("Only an issued invoice or advance invoice can be marked paid.", (IEnumerable<string>?)null, HttpStatusCode.Conflict)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.NotPayable");

    public static CustomException NotIssued()
        => new CustomException("Only an issued document has a PDF, can be sent or converted.", (IEnumerable<string>?)null, HttpStatusCode.Conflict)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.NotIssued");

    public static CustomException NumberingConflict()
        => new CustomException("The document number could not be reserved, try again.", (IEnumerable<string>?)null, HttpStatusCode.Conflict)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.NumberingConflict");

    public static CustomException CustomerEmailMissing()
        => new CustomException("The customer has no e-mail address and none was given.", (IEnumerable<string>?)null, HttpStatusCode.UnprocessableEntity)
            .Localized<InvoiceDocumentsResources>("InvoiceDocuments.CustomerEmailMissing");
}
