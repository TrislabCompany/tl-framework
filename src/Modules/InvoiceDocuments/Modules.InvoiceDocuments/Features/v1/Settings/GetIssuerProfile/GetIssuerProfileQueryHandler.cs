using Mediator;
using Microsoft.EntityFrameworkCore;
using Trislab.Modules.InvoiceDocuments.Contracts.Dtos;
using Trislab.Modules.InvoiceDocuments.Contracts.v1.Settings;
using Trislab.Modules.InvoiceDocuments.Data;
using Trislab.Modules.InvoiceDocuments.Services;

namespace Trislab.Modules.InvoiceDocuments.Features.v1.Settings.GetIssuerProfile;

public sealed class GetIssuerProfileQueryHandler(InvoiceDocumentsDbContext dbContext)
    : IQueryHandler<GetIssuerProfileQuery, IssuerProfileDto?>
{
    public async ValueTask<IssuerProfileDto?> Handle(GetIssuerProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var profile = await dbContext.IssuerProfiles.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return profile?.ToDto();
    }
}
