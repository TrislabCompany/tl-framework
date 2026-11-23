using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Domain;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Settings.UpdateIssuerProfile;

public sealed class UpdateIssuerProfileCommandHandler(InvoiceDocumentsDbContext dbContext)
    : ICommandHandler<UpdateIssuerProfileCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateIssuerProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profile = await dbContext.IssuerProfiles.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            profile = IssuerProfile.Create(
                command.Name, command.AddressLine, command.PostalCode, command.City, command.CountryCode,
                command.TaxNumber, command.Iban, command.Email, command.DefaultCurrency, command.DefaultPaymentTermDays);
            dbContext.IssuerProfiles.Add(profile);
        }
        else
        {
            // Documents already issued keep the details they were issued with.
            profile.Update(
                command.Name, command.AddressLine, command.PostalCode, command.City, command.CountryCode,
                command.TaxNumber, command.Iban, command.Email, command.DefaultCurrency, command.DefaultPaymentTermDays);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return profile.Id;
    }
}
