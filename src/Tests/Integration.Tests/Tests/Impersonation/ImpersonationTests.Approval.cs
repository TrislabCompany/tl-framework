#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable S3459 // Unassigned members should be removed
using System.Net.Http.Json;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trislab.Modules.Identity.Data;

namespace Integration.Tests.Tests.Impersonation;

/// <summary>
/// D-44: impersonation starts only from a request its user approved, once, and only reads. The user can
/// deny a request, take an approval back, or end the impersonation the request started.
/// </summary>
public sealed partial class ImpersonationTests
{
    // ─── Approval ───────────────────────────────────────────────────────

    [Fact]
    public async Task Start_Should_Return403_When_RequestIsNotApproved()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "waiting for the answer");

        var response = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(response)).ShouldBe("Identity.ImpersonationNotApproved");
    }

    [Fact]
    public async Task Start_Should_UseAnApprovalOnce()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await ApprovedRequestAsync(rootClient, _tenantAdminUserId, _tenantId, "one session only");

        var first = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });
        var second = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Start_Should_Return404_When_RequestBelongsToAnotherActor()
    {
        // A tenant admin's own approved request can't be started by the root operator.
        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var basic = await TestUsers.InviteAndAcceptAsync(_factory, tenantAdminClient, "impother");
        using var basicClient = await _auth.CreateAuthenticatedClientAsync(basic.Email, basic.Password, _tenantId);
        var requestId = await ApprovedRequestAsync(tenantAdminClient, basic.UserId, _tenantId, "tenant support", basicClient);

        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var response = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Approve_Should_Return404_When_CallerIsNotTheRequestedUser()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "only the admin may answer");

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var basic = await TestUsers.InviteAndAcceptAsync(_factory, tenantAdminClient, "impnosy");
        using var basicClient = await _auth.CreateAuthenticatedClientAsync(basic.Email, basic.Password, _tenantId);

        // Another member of the tenant, and the operator itself, can't approve it.
        var byOtherMember = await basicClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/approve", content: null);
        var byOperator = await rootClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/approve", content: null);

        byOtherMember.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        byOperator.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var start = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });
        start.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Approve_Should_Return409_When_RequestIsNotPending()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await ApprovedRequestAsync(rootClient, _tenantAdminUserId, _tenantId, "approved twice");

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var again = await tenantAdminClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/approve", content: null);

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deny_Should_BlockTheStart()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "denied");

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var deny = await tenantAdminClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/deny", content: null);
        var start = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        deny.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await deny.Content.ReadFromJsonAsync<ImpersonationRequestPayload>(Json))!.Status.ShouldBe("Denied");
        start.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deny_Should_TakeAnApprovalBack()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await ApprovedRequestAsync(rootClient, _tenantAdminUserId, _tenantId, "approval taken back");

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        (await tenantAdminClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/deny", content: null))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var start = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        start.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deny_After_Start_Should_EndTheImpersonation()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await ApprovedRequestAsync(rootClient, _tenantAdminUserId, _tenantId, "ended by the user");
        var start = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });
        start.EnsureSuccessStatusCode();
        var token = (await start.Content.ReadFromJsonAsync<ImpersonationResponse>(Json))!.AccessToken;
        using var impersonated = ClientWithBearer(token, _tenantId);
        (await impersonated.GetAsync($"{TestConstants.IdentityBasePath}/profile")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var mine = await tenantAdminClient.GetFromJsonAsync<List<ImpersonationRequestPayload>>(
            $"{ImpersonationBasePath}/requests/mine", Json);
        mine!.ShouldContain(r => r.Id == requestId && r.Status == "Used" && r.GrantId != null);

        var deny = await tenantAdminClient.PostAsync($"{ImpersonationBasePath}/requests/{requestId}/deny", content: null);
        deny.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The grant is revoked, so the token in flight stops working.
        (await impersonated.GetAsync($"{TestConstants.IdentityBasePath}/profile")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Mine_Should_ListAPendingRequest_And_Sent_Should_ListItForTheOperator()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "listed both ways");

        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var mine = await tenantAdminClient.GetFromJsonAsync<List<ImpersonationRequestPayload>>(
            $"{ImpersonationBasePath}/requests/mine", Json);
        var sent = await rootClient.GetFromJsonAsync<List<ImpersonationRequestPayload>>(
            $"{ImpersonationBasePath}/requests?Status=Pending", Json);

        mine!.ShouldContain(r => r.Id == requestId && r.Status == "Pending" && r.Reason == "listed both ways");
        sent!.ShouldContain(r => r.Id == requestId);
        // The requests to other users are not the admin's to see.
        mine!.ShouldAllBe(r => r.TargetUserId == _tenantAdminUserId && r.TargetTenantId == _tenantId);
    }

    // ─── Read only ──────────────────────────────────────────────────────

    [Fact]
    public async Task Impersonation_Should_Read_But_Not_Write()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var token = await StartImpersonationAsync(rootClient, _tenantAdminUserId, _tenantId);
        using var impersonated = ClientWithBearer(token, _tenantId);

        var read = await impersonated.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        var write = await impersonated.PutAsJsonAsync($"{TestConstants.IdentityBasePath}/profile", new
        {
            id = _tenantAdminUserId,
            firstName = "Changed",
            lastName = "ByOperator",
        });
        var invite = await impersonated.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new
        {
            email = $"imp-write-{Guid.NewGuid():N}@example.com",
        });
        var sse = await impersonated.PostAsync("/api/v1/sse/token", content: null);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(write)).ShouldBe("Identity.ImpersonationReadOnly");
        invite.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        sse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Ending the impersonation is the one write it may make.
        (await impersonated.PostAsync($"{ImpersonationBasePath}/end", content: null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_Should_Return409_When_AnOpenRequestExists()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        _ = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "first ask");

        var second = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/requests", new
        {
            targetUserId = _tenantAdminUserId,
            targetTenantId = _tenantId,
            reason = "asking again",
        });

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Expired_Requests_Should_Neither_Be_Approved_Nor_Start()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var pending = await RequestAsync(rootClient, _tenantAdminUserId, _tenantId, "never answered");
        await ExpireAsync(pending);
        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var lateApproval = await tenantAdminClient.PostAsync($"{ImpersonationBasePath}/requests/{pending}/approve", content: null);

        var approved = await ApprovedRequestAsync(rootClient, _tenantAdminUserId, _tenantId, "never started");
        await ExpireAsync(approved);
        var lateStart = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId = approved });

        lateApproval.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        lateStart.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_Failed_Start_Should_Keep_The_Approval()
    {
        // Arrange — a Basic member approves, then is deactivated, so the start fails after taking the approval.
        using var tenantAdminClient = await CreateTenantAdminClientAsync();
        var member = await TestUsers.InviteAndAcceptAsync(_factory, tenantAdminClient, "impfail");
        using var memberClient = await _auth.CreateAuthenticatedClientAsync(member.Email, member.Password, _tenantId);
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var requestId = await ApprovedRequestAsync(rootClient, member.UserId, _tenantId, "start fails once", memberClient);
        (await SetActiveAsync(tenantAdminClient, member.UserId, active: false)).ShouldBeTrue();

        // Act
        var failed = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });
        (await SetActiveAsync(tenantAdminClient, member.UserId, active: true)).ShouldBeTrue();
        var retried = await rootClient.PostAsJsonAsync($"{ImpersonationBasePath}/start", new { requestId });

        // Assert — the failure handed the approval back, so the retry starts.
        failed.IsSuccessStatusCode.ShouldBeFalse();
        retried.StatusCode.ShouldBe(HttpStatusCode.OK, await retried.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Impersonation_Should_Connect_To_The_Hub_But_Not_Type_As_The_User()
    {
        using var rootClient = await _auth.CreateRootAdminClientAsync();
        var token = await StartImpersonationAsync(rootClient, _tenantAdminUserId, _tenantId);

        await using var hub = new HubConnectionBuilder()
            .WithUrl(
                $"http://localhost/api/v1/realtime/hub?access_token={Uri.EscapeDataString(token)}",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.WebSocketFactory = (_, _) => throw new NotSupportedException();
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                    options.Headers["tenant"] = _tenantId;
                })
            .Build();
        await hub.StartAndWaitForServerAsync();

        var typing = await Should.ThrowAsync<HubException>(() => hub.InvokeAsync("Typing", Guid.NewGuid()));
        typing.Message.ShouldContain("Impersonation only reads.");
    }

    private async Task ExpireAsync(Guid requestId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.ImpersonationRequests
            .Where(r => r.Id == requestId)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(-1)));
    }

    private static async Task<bool> SetActiveAsync(HttpClient adminClient, string userId, bool active)
    {
        using var response = await adminClient.PatchAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{userId}", new { userId, activateUser = active });
        return response.IsSuccessStatusCode;
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}
