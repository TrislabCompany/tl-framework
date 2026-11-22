using System.Data.Common;
using Trislab.Framework.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Trislab.Framework.Persistence;

/// <summary>
/// Extension methods for configuring Entity Framework DbContextOptionsBuilder.
/// </summary>
public static class OptionsBuilderExtensions
{
    /// <summary>
    /// Configures the database provider and connection for the Hero framework.
    /// </summary>
    /// <param name="builder">The DbContextOptionsBuilder to configure.</param>
    /// <param name="dbProvider">The database provider (PostgreSQL, MSSQL).</param>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <param name="migrationsHistorySchema">The schema of the context's migrations history table (the
    /// module's own schema), or null to keep what the options already say. Migrations live in the
    /// context's own assembly, EF Core's default.</param>
    /// <returns>The configured DbContextOptionsBuilder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when builder is null or dbProvider is null/whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when an unsupported database provider is specified.</exception>
    public static DbContextOptionsBuilder ConfigureTrislabDatabase(
        this DbContextOptionsBuilder builder,
        string dbProvider,
        string connectionString,
        bool isDevelopment,
        string? migrationsHistorySchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(dbProvider);

        ConfigureCommon(builder, isDevelopment);

        switch (dbProvider.ToUpperInvariant())
        {
            case DbProviders.PostgreSQL:
                builder.UseNpgsql(connectionString, e =>
                {
                    // Each module keeps its migrations history in its own schema, so modules never see
                    // each other's history and a module a product leaves out leaves nothing behind.
                    if (!string.IsNullOrWhiteSpace(migrationsHistorySchema))
                    {
                        e.MigrationsHistoryTable(HistoryRepository.DefaultTableName, migrationsHistorySchema);
                    }
                });
                break;

            case DbProviders.MSSQL:
                builder.UseSqlServer(connectionString, e =>
                {
                    // Each module keeps its migrations history in its own schema, so modules never see
                    // each other's history and a module a product leaves out leaves nothing behind.
                    if (!string.IsNullOrWhiteSpace(migrationsHistorySchema))
                    {
                        e.MigrationsHistoryTable(HistoryRepository.DefaultTableName, migrationsHistorySchema);
                    }
                    e.EnableRetryOnFailure();
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Database Provider {dbProvider} is not supported.");
        }

        return builder;
    }

    /// <summary>
    /// Configures the provider against an existing <see cref="DbConnection"/> owned by the DI scope
    /// rather than a connection string.
    ///
    /// Every context in a scope sharing one connection object is what allows the outbox write to
    /// join the business transaction — EF Core can only enlist a context in an existing transaction
    /// when both contexts sit on the same connection.
    /// </summary>
    public static DbContextOptionsBuilder ConfigureTrislabDatabase(
        this DbContextOptionsBuilder builder,
        string dbProvider,
        DbConnection connection,
        bool isDevelopment,
        string? migrationsHistorySchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(dbProvider);

        ConfigureCommon(builder, isDevelopment);

        switch (dbProvider.ToUpperInvariant())
        {
            case DbProviders.PostgreSQL:
                // contextOwnsConnection: false — the scope disposes it, not the first context to finish.
                builder.UseNpgsql(connection, contextOwnsConnection: false, e =>
                {
                    // Each module keeps its migrations history in its own schema, so modules never see
                    // each other's history and a module a product leaves out leaves nothing behind.
                    if (!string.IsNullOrWhiteSpace(migrationsHistorySchema))
                    {
                        e.MigrationsHistoryTable(HistoryRepository.DefaultTableName, migrationsHistorySchema);
                    }
                });
                break;

            case DbProviders.MSSQL:
                builder.UseSqlServer(connection, contextOwnsConnection: false, e =>
                {
                    // Each module keeps its migrations history in its own schema, so modules never see
                    // each other's history and a module a product leaves out leaves nothing behind.
                    if (!string.IsNullOrWhiteSpace(migrationsHistorySchema))
                    {
                        e.MigrationsHistoryTable(HistoryRepository.DefaultTableName, migrationsHistorySchema);
                    }
                    e.EnableRetryOnFailure();
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Database Provider {dbProvider} is not supported.");
        }

        return builder;
    }

    private static void ConfigureCommon(DbContextOptionsBuilder builder, bool isDevelopment)
    {
        builder.ConfigureWarnings(warnings =>
            warnings.Log(RelationalEventId.PendingModelChangesWarning));

        if (isDevelopment)
        {
            builder.EnableSensitiveDataLogging();
            builder.EnableDetailedErrors();
        }
    }
}