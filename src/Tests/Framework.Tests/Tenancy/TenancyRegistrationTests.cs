using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Eventing.Abstractions;
using Trislab.Framework.Quota;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Shared.Quota;
using Trislab.Framework.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Framework.Tests.Tenancy;

public sealed class TenancyRegistrationTests
{
    private static Dictionary<string, string?> SingleTenantSettings(string id = "main") => new()
    {
        ["TenancyOptions:Mode"] = "SingleTenant",
        ["TenancyOptions:SingleTenant:Id"] = id,
        ["TenancyOptions:SingleTenant:Name"] = "Main Organisation",
        ["TenancyOptions:SingleTenant:Country"] = "si",
        ["TenancyOptions:SingleTenant:AdminEmail"] = "admin@main.test",
    };

    private static HostApplicationBuilder BuilderWith(Dictionary<string, string?>? settings)
    {
        var builder = Host.CreateApplicationBuilder();
        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        return builder;
    }

    [Fact]
    public void Mode_Should_DefaultToMultitenant_When_NothingIsConfigured()
    {
        var builder = BuilderWith(null);

        builder.AddTrislabTenancy();

        TenancyOptions.ReadMode(builder.Configuration).ShouldBe(TenancyMode.Multitenant);
        using var provider = builder.Services.BuildServiceProvider();
        provider.GetService<ISingleTenantProvider>().ShouldBeNull();
        provider.GetRequiredService<IEventTenantScope>().ShouldBeOfType<FinbuckleEventTenantScope>();
    }

    [Fact]
    public async Task SingleTenant_Should_AlwaysResolveTheFixedTenant()
    {
        var builder = BuilderWith(SingleTenantSettings());
        builder.AddTrislabTenancy();
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ITenantResolver<AppTenantInfo>>();
        var resolved = await resolver.ResolveAsync(new object());

        resolved.TenantInfo.ShouldNotBeNull();
        resolved.TenantInfo.Id.ShouldBe("main");
        resolved.TenantInfo.Name.ShouldBe("Main Organisation");
        resolved.TenantInfo.IsActive.ShouldBeTrue();
        resolved.TenantInfo.AdminEmail.ShouldBe("admin@main.test");
        scope.ServiceProvider.GetRequiredService<ISingleTenantProvider>().Country.ShouldBe("SI");
        scope.ServiceProvider.GetRequiredService<IEventTenantScope>().ShouldBeOfType<SingleTenantEventTenantScope>();
    }

    [Fact]
    public void SingleTenant_Should_NotExpire()
    {
        var builder = BuilderWith(SingleTenantSettings());
        builder.AddTrislabTenancy();
        using var provider = builder.Services.BuildServiceProvider();

        var tenant = provider.GetRequiredService<ISingleTenantProvider>().Tenant;

        // Identity adds a grace period to ValidUpto; it must not overflow or ever be in the past.
        tenant.ValidUpto.AddDays(3650).ShouldBeGreaterThan(DateTime.UtcNow.AddYears(100));
    }

    [Fact]
    public void QuotaService_Should_BeTheNoopOne_InSingleTenantMode()
    {
        var settings = SingleTenantSettings();
        settings["QuotaOptions:Enabled"] = "true";
        var builder = BuilderWith(settings);
        builder.AddTrislabTenancy();
        builder.Services.AddTrislabQuotas(builder.Configuration);
        using var provider = builder.Services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IQuotaService>().ShouldBeOfType<NoopQuotaService>();
    }

    [Fact]
    public void Code_Should_Win_OverConfiguration()
    {
        var builder = BuilderWith(null);

        builder.AddTrislabTenancy(t => t.UseSingleTenant(o =>
        {
            o.Id = "app";
            o.Name = "App";
            o.Country = "SI";
            o.AdminEmail = "admin@app.test";
        }));

        TenancyOptions.ReadMode(builder.Configuration).ShouldBe(TenancyMode.SingleTenant);
        using var provider = builder.Services.BuildServiceProvider();
        provider.GetRequiredService<ISingleTenantProvider>().Tenant.Id.ShouldBe("app");
    }

    [Fact]
    public void Start_Should_Fail_WithAClearMessage_When_TheFixedTenantIsInvalid()
    {
        var settings = SingleTenantSettings(id: "root");
        var builder = BuilderWith(settings);

        var failure = Should.Throw<InvalidOperationException>(() => builder.AddTrislabTenancy());

        failure.Message.ShouldContain("TenancyOptions:SingleTenant");
        failure.Message.ShouldContain("root");
    }
}
