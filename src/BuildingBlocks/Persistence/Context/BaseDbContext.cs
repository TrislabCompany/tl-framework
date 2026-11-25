using Finbuckle.MultiTenant.EntityFrameworkCore;
using Trislab.Framework.Core.Domain;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Trislab.Framework.Persistence.Context;

/// <summary>
/// Base database context with multi-tenancy and soft delete support.
/// </summary>
public class BaseDbContext : MultiTenantDbContext<string>
{
    private readonly DatabaseOptions _settings;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Creates the context for the current tenant. The tenant is captured now: a context created outside any
    /// tenant sees no tenant-scoped rows.
    /// </summary>
    /// <param name="tenants">The tenant of the current asynchronous flow.</param>
    /// <param name="options">Database context options.</param>
    /// <param name="settings">Database configuration settings.</param>
    /// <param name="environment">Host environment information.</param>
    public BaseDbContext(
        ITenantAccessor tenants,
        DbContextOptions options,
        IOptions<DatabaseOptions> settings,
        IHostEnvironment environment)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        ArgumentNullException.ThrowIfNull(settings);
        TenantInfo = tenants.TenantInfo;
        _settings = settings.Value;
        _environment = environment;
    }

    /// <summary>
    /// Configures the model by applying global query filters for soft delete functionality.
    /// </summary>
    /// <param name="modelBuilder">The model builder used to configure the database schema.</param>
    /// <exception cref="ArgumentNullException">Thrown when modelBuilder is null.</exception>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.AppendGlobalQueryFilter<ISoftDeletable>(QueryFilters.SoftDelete, s => !s.IsDeleted);
        base.OnModelCreating(modelBuilder);
        // Default-on tenant isolation: entities not marked IGlobalEntity get IsMultiTenant().
        // Subclasses must call base.OnModelCreating AFTER ApplyConfigurationsFromAssembly so per-entity configs are in place.
        modelBuilder.ApplyTenantIsolationByDefault();
    }

    /// <summary>
    /// Configures the database connection using tenant-specific connection string if available.
    /// </summary>
    /// <param name="optionsBuilder">The options builder for configuring the database connection.</param>
    /// <exception cref="ArgumentNullException">Thrown when optionsBuilder is null.</exception>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        var tenantConnectionString = (TenantInfo as AppTenantInfo)?.ConnectionString;
        if (string.IsNullOrWhiteSpace(tenantConnectionString))
        {
            return;
        }

        // Route the tenant connection through the scope's connection provider too. Opening a
        // private connection here would defeat the sharing that lets an outbox write join the
        // business transaction — a tenant-database context would be the one case that silently
        // lost atomicity. Falls back to the connection string when no provider is available
        // (hand-constructed contexts in tests, design-time tooling).
        var connectionProvider = optionsBuilder.Options
            .FindExtension<CoreOptionsExtension>()?
            .ApplicationServiceProvider?
            .GetService<IScopedDbConnectionProvider>();

        if (connectionProvider is null)
        {
            optionsBuilder.ConfigureTrislabDatabase(
                _settings.Provider,
                tenantConnectionString,
                _environment.IsDevelopment());
            return;
        }

        optionsBuilder.ConfigureTrislabDatabase(
            _settings.Provider,
            connectionProvider.GetConnection(_settings.Provider, tenantConnectionString),
            _environment.IsDevelopment());
    }

    /// <summary>
    /// Saves all changes made in this context to the database with tenant overwrite mode.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TenantNotSetMode = TenantNotSetMode.Overwrite;
        int result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}