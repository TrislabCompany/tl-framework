using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Framework.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Framework.Tests.Tenancy;

public sealed class FinbuckleEventTenantScopeTests
{
    private readonly AmbientTenantAccessor _accessor = new(new AmbientTenantContext<AppTenantInfo, string>());

    [Fact]
    public void Begin_Should_SetTenantContext_When_TenantIdProvided()
    {
        var sut = CreateScope();

        using (sut.Begin("acme"))
        {
            _accessor.TenantInfo.ShouldNotBeNull();
            _accessor.TenantInfo.Id.ShouldBe("acme");
            _accessor.TenantInfo.Identifier.ShouldBe("acme");
        }
    }

    [Fact]
    public void Begin_Should_RestorePreviousContext_When_Disposed()
    {
        // Seed a pre-existing ambient tenant.
        using var root = _accessor.Use(new AppTenantInfo("root", "root"));
        var sut = CreateScope();

        using (sut.Begin("acme"))
        {
            _accessor.TenantInfo!.Id.ShouldBe("acme");
        }

        _accessor.TenantInfo.Id.ShouldBe("root");
    }

    [Fact]
    public void Begin_Should_LeaveContextUntouched_When_TenantIdNullOrWhitespace()
    {
        // Seed a known tenant so we can prove Begin(null/"") does not replace it.
        using var root = _accessor.Use(new AppTenantInfo("root", "root"));
        var sut = CreateScope();

        using (sut.Begin(null))
        {
            _accessor.TenantInfo!.Id.ShouldBe("root");
        }

        using (sut.Begin("   "))
        {
            _accessor.TenantInfo.Id.ShouldBe("root");
        }
    }

    private FinbuckleEventTenantScope CreateScope()
        => new(_accessor, Substitute.For<IServiceScopeFactory>(), NullLogger<FinbuckleEventTenantScope>.Instance);

}
