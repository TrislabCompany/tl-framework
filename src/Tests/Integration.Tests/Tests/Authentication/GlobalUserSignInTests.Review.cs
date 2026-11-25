using Microsoft.AspNetCore.Identity;
using Hangfire;
using Hangfire.Storage;
using Trislab.Framework.Jobs.Mail;
using Integration.Tests.Infrastructure;
using Trislab.Modules.Auditing.Persistence;
using Trislab.Modules.Identity.Data;
using Trislab.Modules.Identity.Domain;

namespace Integration.Tests.Tests.Authentication;

/// <summary>The second security review of global users: sessions, enumeration and where account events are audited.</summary>
public sealed partial class GlobalUserSignInTests
{
    // ---- A revoked session can't open another ------------------------------------------------------------

    [Fact]
    public async Task A_Token_Of_A_Signed_Out_Session_Cannot_Switch_Tenant()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swrevoked");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        using var client = Bearer(signedIn.AccessToken!);

        // Signing out everywhere ends this session too; its access token is still within its lifetime.
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/sessions/revoke-all", new { })).IsSuccessStatusCode.ShouldBeTrue();

        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_Token_Of_A_Removed_Membership_Cannot_Switch_To_Another_Tenant()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swremoved");
        var inA = await SignInAsync(user.Email, user.Password, tenantA);
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);
        (await adminA.DeleteAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var client = Bearer(inA.AccessToken!);
        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB });
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_Live_Session_Still_Switches_Tenant()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swlive");
        using var client = Bearer((await SignInAsync(user.Email, user.Password, tenantA)).AccessToken!);

        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    // ---- Sign-in enumeration of invited accounts ---------------------------------------------------------

    [Fact]
    public async Task An_Invited_Address_Signs_In_Like_An_Unknown_One_And_Counts_No_Failure()
    {
        var tenantId = await CreateTenantAsync("pendingenum");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"pending-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var userId = await TestUsers.FindUserIdAsync(_factory, email);

        using var invited = await IssueAsync(email, "Any@12345!");
        using var unknown = await IssueAsync($"nobody-{Guid.NewGuid():N}@example.com", "Any@12345!");

        invited.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await invited.Content.ReadAsStringAsync()).ShouldBe(await unknown.Content.ReadAsStringAsync());

        using var scope = _factory.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().FindAccountAsync(userId);
        account!.AccessFailedCount.ShouldBe(0, "an account without a password has nothing to guess");
    }

    [Fact]
    public async Task Inviting_A_New_And_An_Existing_Address_Answers_The_Same()
    {
        var (_, _, existing) = await CreateUserInTwoTenantsAsync("inviteenum");
        var tenantC = await CreateTenantAsync("inviteenumc");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantC), TestConstants.DefaultPassword, tenantC);

        using var forExisting = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new { email = existing.Email });
        using var forNew = await admin.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new { email = $"fresh-{Guid.NewGuid():N}@example.com" });

        forExisting.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        forNew.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        PropertyNames(await forExisting.Content.ReadAsStringAsync()).ShouldBe(PropertyNames(await forNew.Content.ReadAsStringAsync()));
    }

    // ---- Account events are audited in the account's own tenants -----------------------------------------

    [Fact]
    public async Task A_Lockout_Is_Audited_In_The_Accounts_Tenants_And_Never_In_A_Tenant_The_Caller_Names()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("lockaudit");
        var foreign = await CreateTenantAsync("lockforeign");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await IssueAsync(user.Email, "Wrong@1234!", foreign)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var tenants = await WaitForAuditTenantsAsync("TooManyFailedSignIns", user.UserId, expected: 2);
        tenants.ShouldBe(new[] { tenantA, tenantB }, ignoreOrder: true);
        tenants.ShouldNotContain(foreign);
    }

    [Fact]
    public async Task A_Password_Reset_Is_Audited_In_Every_Tenant_Of_The_Account()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("resetaudit");
        var resetToken = await ResetTokenAsync(user.Email);

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = user.Email, password = "Audit@1234!", token = resetToken }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await WaitForAuditTenantsAsync("PasswordReset", user.UserId, expected: 2)).ShouldBe(new[] { tenantA, tenantB }, ignoreOrder: true);
    }

    [Fact]
    public async Task Enabling_And_Disabling_Two_Factor_Is_Audited()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("2faaudit");
        var client = Bearer((await SignInAsync(user.Email, user.Password, tenantA)).AccessToken!);
        client.DefaultRequestHeaders.Add("tenant", tenantA);
        using (client)
        {
            (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/2fa/enroll", new { })).EnsureSuccessStatusCode();

            string code;
            using (var scope = _factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
                var account = (await users.FindByEmailAsync(user.Email))!;
                code = Totp((await users.GetAuthenticatorKeyAsync(account))!, DateTimeOffset.UtcNow);
            }

            using var verify = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/2fa/verify", new { code });
            verify.IsSuccessStatusCode.ShouldBeTrue(await verify.Content.ReadAsStringAsync());
            await WaitForAuditAsync("TwoFactorEnabled", user.UserId);

            using var disable = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/2fa/disable", new { currentPassword = user.Password });
            disable.IsSuccessStatusCode.ShouldBeTrue(await disable.Content.ReadAsStringAsync());
            await WaitForAuditAsync("TwoFactorDisabled", user.UserId);
        }
    }

    [Fact]
    public async Task Revoking_A_Role_And_Deactivating_A_Membership_Are_Audited()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("roleaudit");
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);

        object Roles(bool enabled) => new { userId = user.UserId, userRoles = new[] { new { roleName = "Admin", enabled } } };
        (await adminA.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}/roles", Roles(true))).IsSuccessStatusCode.ShouldBeTrue();
        (await adminA.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}/roles", Roles(false))).IsSuccessStatusCode.ShouldBeTrue();
        await WaitForAuditAsync("RoleRevoked", user.UserId);

        using var deactivate = new HttpRequestMessage(HttpMethod.Patch, $"{TestConstants.IdentityBasePath}/users/{user.UserId}")
        {
            Content = JsonContent.Create(new { isActive = false }),
        };
        (await adminA.SendAsync(deactivate)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForAuditAsync("MembershipDeactivated", user.UserId);
    }

    [Fact]
    public async Task An_Expired_Invitation_Is_Refused_And_Audited()
    {
        var tenantId = await CreateTenantAsync("expired");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"expired-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var token = await TestUsers.WaitForInvitationTokenAsync(email);
        var userId = await TestUsers.FindUserIdAsync(_factory, email);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().MembershipInvitations
                .IgnoreQueryFilters()
                .Where(i => i.UserId == userId)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.ExpiresOnUtc, DateTime.UtcNow.AddMinutes(-1)));
        }

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Chosen@1234!" }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WaitForAuditAsync("InvitationExpired", userId);
    }

    // The code an authenticator app shows for a Base32 key (RFC 6238: HMAC-SHA1, 30-second steps, 6 digits).
    private static string Totp(string base32Key, DateTimeOffset now)
    {
        const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = string.Concat(base32Key.TrimEnd('=').ToUpperInvariant()
            .Select(c => Convert.ToString(Alphabet.IndexOf(c, StringComparison.Ordinal), 2).PadLeft(5, '0')));
        var key = Enumerable.Range(0, bits.Length / 8).Select(i => Convert.ToByte(bits.Substring(i * 8, 8), 2)).ToArray();

        var step = BitConverter.GetBytes(now.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(step);
        }

#pragma warning disable CA5350 // RFC 6238 and every authenticator app use HMAC-SHA1; ASP.NET Identity verifies the same.
        var hash = System.Security.Cryptography.HMACSHA1.HashData(key, step);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    // The tenants that hold an audit record with this reason about this subject, once at least `expected` of them do
    // (then once more after a short wait, so a stray extra record still reaches the caller's assertion).
    private async Task<List<string>> WaitForAuditTenantsAsync(string reasonCode, string subjectId, int expected)
    {
        for (var attempt = 0; attempt < 75; attempt++)
        {
            if ((await AuditTenantsAsync(reasonCode, subjectId)).Count >= expected)
            {
                await Task.Delay(500);
                break;
            }

            await Task.Delay(200);
        }

        return await AuditTenantsAsync(reasonCode, subjectId);
    }

    private async Task<List<string>> AuditTenantsAsync(string reasonCode, string subjectId)
    {
        using var scope = _factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AuditDbContext>().AuditRecords.IgnoreQueryFilters().AsNoTracking()
            .Where(a => a.Source == "Identity" && a.OccurredAtUtc > DateTime.UtcNow.AddMinutes(-30))
            .Select(a => new { a.TenantId, a.PayloadJson })
            .ToListAsync();
        return rows
            .Where(r => r.PayloadJson != null && r.PayloadJson.Contains(reasonCode, StringComparison.Ordinal)
                && r.PayloadJson.Contains(subjectId, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.TenantId ?? "<none>")
            .Distinct()
            .ToList();
    }

    // ---- E-mailed links never sit in the job store ----------------------------------------------------------

    [Fact]
    public async Task A_Queued_Invitation_Keeps_Its_Link_And_Address_Out_Of_The_Job_Store()
    {
        var tenantId = await CreateTenantAsync("jobstore");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"jobstore-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var token = await TestUsers.WaitForInvitationTokenAsync(email);

        // The job that sent it stays in Hangfire's storage until it expires: what is stored is ciphertext.
        var storage = _factory.Services.GetRequiredService<JobStorage>();
        var monitor = storage.GetMonitoringApi();
        var ids = monitor.SucceededJobs(0, (int)monitor.SucceededListCount()).Select(j => j.Key)
            .Concat(monitor.ProcessingJobs(0, (int)monitor.ProcessingCount()).Select(j => j.Key))
            .Concat(monitor.FailedJobs(0, (int)monitor.FailedCount()).Select(j => j.Key))
            .Concat(monitor.EnqueuedJobs("email", 0, int.MaxValue).Select(j => j.Key))
            .Distinct()
            .ToList();

        using var connection = storage.GetConnection();
        var stored = ids
            .Select(id => connection.GetJobData(id))
            .Where(data => data?.Job is not null)
            .Select(data => (Type: data.Job.Type.Name, Payload: InvocationData.SerializeJob(data.Job).SerializePayload()))
            .ToList();

        stored.ShouldContain(j => j.Type == nameof(SendQueuedMailJob), "the invitation is sent by the encrypted mail job");
        foreach (var secret in new[] { token, Uri.EscapeDataString(token), email })
        {
            stored.ShouldNotContain(j => j.Payload.Contains(secret, StringComparison.Ordinal), "no job holds the invitation link or the address in clear");
        }
    }
}
