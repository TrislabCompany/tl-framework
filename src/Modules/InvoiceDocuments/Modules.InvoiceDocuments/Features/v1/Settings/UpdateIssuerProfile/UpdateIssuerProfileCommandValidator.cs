using FluentValidation;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Settings.UpdateIssuerProfile;

public sealed class UpdateIssuerProfileCommandValidator : AbstractValidator<UpdateIssuerProfileCommand>
{
    public UpdateIssuerProfileCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AddressLine).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.TaxNumber).MaximumLength(32);
        RuleFor(x => x.Iban).MaximumLength(42);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(254).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.DefaultCurrency).NotEmpty().Length(3);
        RuleFor(x => x.DefaultPaymentTermDays).InclusiveBetween(0, 365);
    }
}
