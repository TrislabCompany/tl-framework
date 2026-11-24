using Asp.Versioning;
using Trislab.Framework.Eventing;
using Trislab.Framework.Persistence;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Web.Modules;
using Trislab.Framework.Web.Realtime;
using Trislab.Modules.Files.Contracts;

[assembly: TrislabModule(typeof(TestHost.TestModule), 900)]

namespace TestHost;

/// <summary>
/// The one small module of the test host. It gives the framework's tests what they used to take from the
/// samples, and nothing else:
/// <list type="bullet">
/// <item>a tenant-scoped entity with its own DbContext and schema (<see cref="TestItem"/>), for the eventing, outbox and storage tests;</item>
/// <item>a file access policy for an owner type (<see cref="TestItemFileAccessPolicy"/>);</item>
/// <item>a publisher of <c>UserNotificationRequestedIntegrationEvent</c> (<c>POST /api/v1/test/notifications</c>);</item>
/// <item>a channel membership for the realtime hub (<see cref="TestChannelMembership"/>, <c>/api/v1/test/channels</c>).</item>
/// </list>
/// </summary>
internal sealed class TestModule : IModule
{
    public TenancyModes SupportedTenancyModes => TenancyModes.All;

    public void ConfigureServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddTrislabDbContext<TestDbContext>(TestDbContext.Schema);
        builder.Services.AddScoped<IDbInitializer, TestDbInitializer>();

        builder.Services.AddScoped<IFileAccessPolicy, TestItemFileAccessPolicy>();
        builder.Services.AddIntegrationEventHandlers(typeof(TestModule).Assembly);

        // One in-memory membership serves the hub's two lookups.
        builder.Services.AddSingleton<TestChannelMembership>();
        builder.Services.AddSingleton<IChannelMembershipChecker>(sp => sp.GetRequiredService<TestChannelMembership>());
        builder.Services.AddSingleton<IUserChannelLookup>(sp => sp.GetRequiredService<TestChannelMembership>());
    }

    public void ConfigureMiddleware(IApplicationBuilder app)
    {
        // No middleware.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var versionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        var group = endpoints
            .MapGroup("api/v{version:apiVersion}/test")
            .WithTags("Test")
            .WithApiVersionSet(versionSet)
            .RequireAuthorization();

        group.MapTestChannelEndpoints();
        group.MapTestNotificationEndpoint();
    }
}
