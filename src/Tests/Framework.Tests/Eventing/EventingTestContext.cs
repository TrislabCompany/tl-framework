using Trislab.Framework.Eventing.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Persistence;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Framework.Tests.Eventing;

/// <summary>
/// In-memory SQLite EventingDbContext for store-level unit tests. SQLite has no
/// schemas, so EF folds "framework.OutboxMessages" into a single table name —
/// fine here; schema mapping is asserted separately against the Npgsql model in
/// <see cref="EventingDbContextModelTests"/>.
/// </summary>
internal static class EventingTestContext
{
    public static EventingDbContext CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var accessor = Substitute.For<ITenantAccessor>();
        accessor.TenantInfo.Returns(new AppTenantInfo("test", "test"));

        var options = new DbContextOptionsBuilder<EventingDbContext>()
            .UseSqlite(connection)
            .Options;

        var settings = Options.Create(new DatabaseOptions
        {
            Provider = "postgresql",
            ConnectionString = string.Empty,
        });

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");

        // UseSqlite(DbConnection) does NOT transfer ownership of an externally-supplied
        // connection to EF Core — disposing the DbContext alone would leak the native SQLite
        // handle. SqliteOwnedEventingDbContext takes ownership explicitly and disposes the
        // connection alongside itself, so a plain `await using` at the call site is enough.
        var context = new SqliteOwnedEventingDbContext(accessor, options, settings, environment, connection);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// An <see cref="EventingDbContext"/> that owns the <see cref="SqliteConnection"/> backing
    /// it, so disposing the context also closes and disposes the connection. Needed only because
    /// EF Core's <c>UseSqlite(DbConnection)</c> overload treats an externally-supplied connection
    /// as caller-owned.
    /// </summary>
    private sealed class SqliteOwnedEventingDbContext : EventingDbContext
    {
        private readonly SqliteConnection _connection;

        public SqliteOwnedEventingDbContext(
            ITenantAccessor multiTenantContextAccessor,
            DbContextOptions<EventingDbContext> options,
            IOptions<DatabaseOptions> settings,
            IHostEnvironment environment,
            SqliteConnection connection)
            : base(multiTenantContextAccessor, options, settings, environment)
        {
            _connection = connection;
        }

        public override void Dispose()
        {
            base.Dispose();
            _connection.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
