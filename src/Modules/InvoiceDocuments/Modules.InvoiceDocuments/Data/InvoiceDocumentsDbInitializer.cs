using Trislab.Framework.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Trislab.Modules.InvoiceDocuments.Data;

public sealed class InvoiceDocumentsDbInitializer(
    InvoiceDocumentsDbContext dbContext,
    ILogger<InvoiceDocumentsDbInitializer> logger) : IDbInitializer
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        if ((await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Any())
        {
            await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("[InvoiceDocuments] applied migrations");
        }
    }

    /// <summary>
    /// Nothing is seeded: a tenant starts with no customers, items or numbers, and sets up its own business
    /// details before its first document.
    /// </summary>
    public Task SeedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
