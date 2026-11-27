using System.Security.Claims;
using Trislab.Framework.Shared.Constants;
using Trislab.Modules.Identity.Features.v1.Impersonation.StartImpersonation;
using System.IdentityModel.Tokens.Jwt;

namespace Identity.Tests.Handlers;

/// <summary>
/// The impersonation token must NOT carry the target user's `locale` claim — language is a
/// presentation concern, so the operator keeps reading in their own language. The start itself
/// (the approved request, consumed once) is covered end to end by Integration.Tests ImpersonationTests.
/// </summary>
public sealed class StartImpersonationCommandHandlerTests
{
    private const string TenantId = "codefi";
    private const string TargetUserId = "target-user";

    [Fact]
    public void ImpersonationClaims_strips_locale_but_preserves_identity_claims_and_injects_actor()
    {
        // Arrange — a realistic target claim set: the persisted `locale` must be dropped, but every other
        // identity claim (name, role, subject, tenant) must survive into the impersonation token.
        var actorUserId = Guid.NewGuid().ToString();
        var targetClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, "orig-jti"),
            new(JwtRegisteredClaimNames.Sub, TargetUserId),
            new(ClaimConstants.Tenant, TenantId),
            new(ClaimTypes.Name, "Target User"),
            new(ClaimTypes.Role, "Admin"),
            new("locale", "sl-SI"),
        };

        // Act
        var issued = StartImpersonationCommandHandler.ImpersonationClaims(targetClaims, "new-jti", actorUserId, TenantId);

        // Assert
        // (a) locale is stripped — the operator keeps reading in their own language.
        issued.ShouldNotContain(c => c.Type == "locale");

        // (b) every NON-locale identity claim survives — a mutation that over-strips (e.g. drops Name,
        //     role, sub or tenant) must fail here.
        issued.ShouldContain(c => c.Type == ClaimTypes.Name && c.Value == "Target User");
        issued.ShouldContain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        issued.ShouldContain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == TargetUserId);
        issued.ShouldContain(c => c.Type == ClaimConstants.Tenant && c.Value == TenantId);

        // (c) RFC 8693 actor claims are injected so the token records who is acting.
        issued.ShouldContain(c => c.Type == ClaimConstants.ActorSubject && c.Value == actorUserId);
        issued.ShouldContain(c => c.Type == ClaimConstants.ActorTenant && c.Value == TenantId);

        // (d) the jti is swapped: the target's original jti is gone and exactly the new one is present
        //     (so the persisted grant row and the JWT share one identifier).
        issued.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value.ShouldBe("new-jti");
    }
}
