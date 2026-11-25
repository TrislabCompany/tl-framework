using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Persistence.Context;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Trislab.Modules.InvoiceDocuments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Trislab.Modules.InvoiceDocuments.Data;

/// <summary>
/// The documents a tenant issues to its own customers. A <see cref="BaseDbContext"/>: every row is stamped
/// with the tenant and filtered by it, in both tenancy modes.
/// </summary>
public sealed class InvoiceDocumentsDbContext : BaseDbContext
{
    public const string Schema = "invoicedocuments";

    public InvoiceDocumentsDbContext(
        ITenantAccessor multiTenantContextAccessor,
        DbContextOptions<InvoiceDocumentsDbContext> options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment) : base(multiTenantContextAccessor, options, settings, environment) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<IssuerProfile> IssuerProfiles => Set<IssuerProfile>();
    public DbSet<NumberingSeries> NumberingSeries => Set<NumberingSeries>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentLine> DocumentLines => Set<DocumentLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InvoiceDocumentsDbContext).Assembly);
        // base.OnModelCreating runs LAST so the tenant filter reaches every configured entity.
        base.OnModelCreating(modelBuilder);
    }
}
