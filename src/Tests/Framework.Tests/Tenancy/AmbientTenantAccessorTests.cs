using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Shouldly;
using Xunit;

namespace Framework.Tests.Tenancy;

/// <summary>
/// The tenant of the current flow is what every tenant filter and every tenant database connection is built from,
/// so its rules are pinned here: no tenant outside a scope, the outer tenant back after a nested one, and no
/// tenant shared between concurrent flows.
/// </summary>
public sealed class AmbientTenantAccessorTests
{
    private readonly AmbientTenantAccessor _sut = new(new AmbientTenantContext<AppTenantInfo, string>());

    [Fact]
    public void TenantInfo_Should_BeNull_When_NoTenantWasSet()
    {
        _sut.TenantInfo.ShouldBeNull();
        _sut.IsResolved.ShouldBeFalse();
    }

    [Fact]
    public void Use_Should_SetTheTenant_UntilDisposed()
    {
        var acme = Tenant("acme");

        using (_sut.Use(acme))
        {
            _sut.TenantInfo.ShouldBeSameAs(acme);
            _sut.IsResolved.ShouldBeTrue();
        }

        _sut.TenantInfo.ShouldBeNull();
    }

    [Fact]
    public void Use_Should_RestoreTheOuterTenant_When_ANestedOneIsDisposed()
    {
        var outer = Tenant("outer");
        var inner = Tenant("inner");

        using (_sut.Use(outer))
        {
            using (_sut.Use(inner))
            {
                _sut.TenantInfo.ShouldBeSameAs(inner);
            }

            _sut.TenantInfo.ShouldBeSameAs(outer);
        }
    }

    [Fact]
    public void Use_Should_AllowOneTenantAfterAnother_When_AJobWalksEveryTenant()
    {
        // Finbuckle sets a scope's tenant only once; a job visiting each tenant in turn must still work.
        foreach (var id in new[] { "a", "b", "c" })
        {
            using var tenantUse = _sut.Use(Tenant(id));
            _sut.TenantInfo.ShouldNotBeNull().Id.ShouldBe(id);
        }

        _sut.TenantInfo.ShouldBeNull();
    }

    [Fact]
    public void Use_Should_ClearTheTenant_When_GivenNull()
    {
        using (_sut.Use(Tenant("acme")))
        {
            using (_sut.Use(null))
            {
                _sut.TenantInfo.ShouldBeNull();
                _sut.IsResolved.ShouldBeFalse();
            }

            _sut.TenantInfo.ShouldNotBeNull().Id.ShouldBe("acme");
        }
    }

    [Fact]
    public void Dispose_Should_RestoreOnlyOnce_When_CalledTwice()
    {
        var outer = Tenant("outer");
        using var outerUse = _sut.Use(outer);
        var inner = _sut.Use(Tenant("inner"));

        inner.Dispose();
        using (_sut.Use(Tenant("later")))
        {
            inner.Dispose();
            _sut.TenantInfo.ShouldNotBeNull().Id.ShouldBe("later");
        }

        _sut.TenantInfo.ShouldBeSameAs(outer);
    }

    [Fact]
    public async Task Use_Should_NotLeakBetweenConcurrentFlows()
    {
        async Task<string?> RunAsAsync(string id)
        {
            using var tenantUse = _sut.Use(Tenant(id));
            // Both flows hold their tenant at the same time.
            await Task.Delay(50, TestContext.Current.CancellationToken);
            return _sut.TenantInfo?.Id;
        }

        var results = await Task.WhenAll(RunAsAsync("a"), RunAsAsync("b"));

        results.ShouldBe(["a", "b"]);
        _sut.TenantInfo.ShouldBeNull("a child flow's tenant must not reach its caller");
    }

    [Fact]
    public void Constructor_Should_Throw_When_TheContextIsNotFinbucklesAmbientOne()
    {
        Should.Throw<InvalidOperationException>(() => new AmbientTenantAccessor(new OtherContext()));
    }

    private static AppTenantInfo Tenant(string id) => new(id, id);

    private sealed class OtherContext : ITenantContext<AppTenantInfo, string>
    {
        public AppTenantInfo? TenantInfo { get; set; }

        public bool IsResolved => TenantInfo is not null;

        ITenantInfo<string>? ITenantContext<string>.TenantInfo
        {
            get => TenantInfo;
            set => TenantInfo = (AppTenantInfo?)value;
        }
    }
}
