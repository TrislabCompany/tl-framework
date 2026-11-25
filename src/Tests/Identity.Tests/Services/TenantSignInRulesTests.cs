using Trislab.Framework.Core.Exceptions;
using Trislab.Modules.Identity.Domain;
using Trislab.Modules.Identity.Services;
using Microsoft.AspNetCore.DataProtection;

namespace Identity.Tests.Services;

/// <summary>The decisions of global-user sign-in, without a database: which tenant opens, the app boundary,
/// the selection token, and the states of memberships and invitations.</summary>
public sealed class TenantSignInRulesTests
{
    private static readonly string[] Two = ["acme", "beta"];

    [Fact]
    public void An_Operator_Always_Opens_Root_Whatever_Was_Requested()
    {
        var decision = TenantSignInService.ChooseTenant(["root"], ["root"], defaultTenant: null, requestedTenant: "acme", multitenant: true);

        decision.ShouldBe(new TenantDecision(TenantDecisionKind.Open, "root"));
    }

    [Fact]
    public void A_Requested_Tenant_Opens_Only_For_A_Member()
    {
        TenantSignInService.ChooseTenant(Two, Two, null, "beta", multitenant: true)
            .ShouldBe(new TenantDecision(TenantDecisionKind.Open, "beta"));
        TenantSignInService.ChooseTenant(Two, Two, null, "other", multitenant: true).Kind
            .ShouldBe(TenantDecisionKind.Refuse, "a tenant the user doesn't belong to is the 401 of a wrong password");
    }

    [Fact]
    public void Single_Tenant_Mode_Ignores_The_Requested_Tenant()
    {
        TenantSignInService.ChooseTenant(["main"], ["main"], null, "other", multitenant: false)
            .ShouldBe(new TenantDecision(TenantDecisionKind.Open, "main"));
    }

    [Fact]
    public void The_Default_Opens_Else_The_Only_Tenant_Else_The_Chooser()
    {
        TenantSignInService.ChooseTenant(Two, Two, "beta", null, true).ShouldBe(new TenantDecision(TenantDecisionKind.Open, "beta"));
        TenantSignInService.ChooseTenant(["acme"], ["acme"], null, null, true).ShouldBe(new TenantDecision(TenantDecisionKind.Open, "acme"));
        TenantSignInService.ChooseTenant(Two, Two, null, null, true).Kind.ShouldBe(TenantDecisionKind.Choose);
        TenantSignInService.ChooseTenant(Two, Two, "gone", null, true).Kind.ShouldBe(TenantDecisionKind.Choose, "a default the user lost is ignored");
    }

    [Fact]
    public void No_Usable_Tenant_Is_Told_Apart_From_No_Membership()
    {
        TenantSignInService.ChooseTenant([], [], null, null, true).Kind.ShouldBe(TenantDecisionKind.Refuse);
        TenantSignInService.ChooseTenant(["acme"], [], null, null, true).Kind.ShouldBe(TenantDecisionKind.NoUsableTenant);
    }

    [Theory]
    [InlineData("root", "dashboard", true)]
    [InlineData("acme", "admin", true)]
    [InlineData("root", "admin", false)]
    [InlineData("acme", "dashboard", false)]
    [InlineData("acme", null, false)]
    public void The_App_Boundary_Holds_In_Multitenant_Mode(string tenant, string? app, bool refused)
    {
        var act = () => TenantSignInService.EnsureAppBoundary(app, tenant, multitenant: true);

        if (refused)
        {
            act.ShouldThrow<ForbiddenException>();
        }
        else
        {
            act.ShouldNotThrow();
        }
    }

    [Fact]
    public void The_Chooser_Refuses_The_Admin_App()
    {
        Should.Throw<ForbiddenException>(() => TenantSignInService.EnsureAppBoundary("admin", tenantId: null, multitenant: true));
    }

    [Fact]
    public void Single_Tenant_Mode_Has_No_App_Boundary()
    {
        Should.NotThrow(() => TenantSignInService.EnsureAppBoundary("admin", "main", multitenant: false));
    }

    [Fact]
    public void A_Selection_Token_Reads_Back_Until_It_Expires()
    {
        var clock = new ManualClock(new DateTimeOffset(2026, 11, 25, 10, 0, 0, TimeSpan.Zero));
        var codec = new SelectionTokenCodec(new EphemeralDataProtectionProvider(), clock);
        var payload = new SelectionTokenPayload(Guid.NewGuid(), "user-1", "ABCD", clock.GetUtcNow().UtcDateTime.Add(SelectionTokenCodec.Lifetime));
        var token = codec.Protect(payload);

        codec.Read(token).ShouldBe(payload);

        clock.Advance(SelectionTokenCodec.Lifetime);
        codec.Read(token).ShouldBeNull("an expired selection token must not work");
    }

    [Fact]
    public void A_Tampered_Or_Foreign_Selection_Token_Reads_As_Null()
    {
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var provider = new EphemeralDataProtectionProvider();
        var codec = new SelectionTokenCodec(provider, clock);
        var token = codec.Protect(new SelectionTokenPayload(Guid.NewGuid(), "user-1", "ABCD", clock.GetUtcNow().UtcDateTime.AddMinutes(5)));

        codec.Read(token[..^2] + "xx").ShouldBeNull();
        codec.Read("not-a-token").ShouldBeNull();
        codec.Read(provider.CreateProtector("Another.Purpose").Protect("x\nuser-1\nABCD\n0")).ShouldBeNull();
    }

    [Fact]
    public void A_Pending_Membership_Is_Activated_Only_By_Acceptance()
    {
        var membership = TenantMembership.CreatePending("user-1", "acme", invitedBy: "admin-1");

        membership.IsPending.ShouldBeTrue();
        membership.IsActive.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => membership.Activate("admin-1"));

        membership.Accept(DateTime.UtcNow);
        membership.IsActive.ShouldBeTrue();
        membership.IsPending.ShouldBeFalse();
    }

    [Fact]
    public void An_Invitation_Expires_And_Can_Be_Accepted_Only_While_Pending()
    {
        var now = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);
        var invitation = MembershipInvitation.Create(Guid.NewGuid(), "user-1", "a@example.com", "HASH", "admin-1", now, TimeSpan.FromHours(1));

        invitation.StatusAt(now).ShouldBe(InvitationStatus.Pending);
        invitation.StatusAt(now.AddHours(1)).ShouldBe(InvitationStatus.Expired);
        Should.Throw<InvalidOperationException>(() => invitation.MarkAccepted(now.AddHours(2)));

        invitation.MarkAccepted(now.AddMinutes(5));
        invitation.StatusAt(now.AddMinutes(6)).ShouldBe(InvitationStatus.Accepted);
        invitation.Revoke("admin-1", now.AddMinutes(7)).ShouldBeFalse("an accepted invitation stays accepted");
    }

    [Fact]
    public void A_Revoked_Invitation_Stays_Revoked()
    {
        var now = DateTime.UtcNow;
        var invitation = MembershipInvitation.Create(Guid.NewGuid(), "user-1", "a@example.com", "HASH", "admin-1", now, TimeSpan.FromDays(7));

        invitation.Revoke("admin-1", now).ShouldBeTrue();
        invitation.StatusAt(now).ShouldBe(InvitationStatus.Revoked);
        Should.Throw<InvalidOperationException>(() => invitation.MarkAccepted(now));
    }

    [Theory]
    [InlineData("root", new[] { "acme" }, true)]
    [InlineData("acme", new[] { "root" }, true)]
    [InlineData("acme", new[] { "beta" }, false)]
    [InlineData("root", new string[0], false)]
    public void Operators_Stay_In_Root(string joining, string[] others, bool broken)
    {
        MembershipRules.IsRootRuleBroken(joining, others).ShouldBe(broken);
    }

    [Fact]
    public void The_Invitation_Token_Is_Stored_Only_As_A_Hash()
    {
        var hash = MembershipInvitationService.HashToken("secret-token");

        hash.ShouldNotContain("secret-token");
        hash.Length.ShouldBe(64);
        MembershipInvitationService.HashToken("secret-token").ShouldBe(hash);
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
