using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Trislab.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Trislab.Modules.Auditing.Persistence;
using Trislab.Modules.Identity.Data;

namespace Integration.Tests.Tests.Authentication;

/// <summary>
/// The hardening around global users: invitations that only the owner can accept, single-use selection
/// tokens, refresh and switch that re-check everything, and refresh-token reuse that ends the session.
/// </summary>
public sealed partial class GlobalUserSignInTests
{
    // ---- Invitations -------------------------------------------------------------------------------------

    [Fact]
    public async Task Invite_Answers_The_Same_For_A_New_And_An_Existing_Address_And_Hides_Pending_Invitees()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("hide");
        var tenantC = await CreateTenantAsync("hidec");
        using var adminC = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantC), TestConstants.DefaultPassword, tenantC);

        using var existing = await adminC.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new { email = user.Email });
        using var fresh = await adminC.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations", new { email = $"fresh-{Guid.NewGuid():N}@example.com" });

        existing.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        fresh.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        PropertyNames(await existing.Content.ReadAsStringAsync()).ShouldBe(PropertyNames(await fresh.Content.ReadAsStringAsync()));
        (await existing.Content.ReadAsStringAsync()).ShouldNotContain(user.UserId, Case.Insensitive);

        // Until the owner accepts, tenant C knows nothing about the account behind the address.
        (await adminC.GetAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var search = await adminC.GetStringAsync($"{TestConstants.IdentityBasePath}/users/search?pageNumber=1&pageSize=50");
        search.ShouldNotContain(user.Email, Case.Insensitive);

        // A pending membership can't be activated by the administrator.
        using var activate = new HttpRequestMessage(HttpMethod.Patch, $"{TestConstants.IdentityBasePath}/users/{user.UserId}")
        {
            Content = JsonContent.Create(new { activateUser = true }),
        };
        (await adminC.SendAsync(activate)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await IssueAsync(user.Email, user.Password, tenantC)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        tenantA.ShouldNotBe(tenantC);
    }

    [Fact]
    public async Task Only_The_Signed_In_Owner_Accepts_An_Invitation_For_An_Existing_Account()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("owner");
        var tenantC = await CreateTenantAsync("ownerc");
        using var adminC = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantC), TestConstants.DefaultPassword, tenantC);
        var token = await InviteExistingAsync(adminC, user.Email);

        // Anonymous, even with a password in the body: refused, and the password is not changed.
        using var anonymous = _factory.CreateClient();
        using var noSignIn = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Hijack@1234!" });
        noSignIn.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await noSignIn.Content.ReadAsStringAsync()).ShouldContain("Identity.InvitationSignInRequired");
        (await IssueAsync(user.Email, "Hijack@1234!", tenantA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Another account (tenant C's admin) can't accept it either.
        using var otherAccount = await adminC.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token });
        otherAccount.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // The owner can; the token then works no more.
        var signedIn = await SignInAsync(user.Email, user.Password, tenantB);
        using var owner = Bearer(signedIn.AccessToken!);
        (await owner.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        TenantClaim((await SignInAsync(user.Email, user.Password, tenantC)).AccessToken!).ShouldBe(tenantC);

        await WaitForAuditAsync("InvitationAccepted", user.UserId);
        await WaitForAuditAsync("MembershipAdded", user.UserId);
        await WaitForAuditAsync("InvitationSent", user.UserId);
    }

    [Fact]
    public async Task A_New_Account_Chooses_Its_Own_Password_And_A_Weak_One_Keeps_The_Invitation()
    {
        var tenantId = await CreateTenantAsync("newacc");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"newacc-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email, "New", "Person");
        var token = await TestUsers.WaitForInvitationTokenAsync(email);

        // Before accepting, the account has no password: nobody, the administrator included, can sign in.
        (await IssueAsync(email, TestConstants.DefaultPassword)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest, "a new account must choose a password");
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "weak" })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Chosen@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        TenantClaim((await SignInAsync(email, "Chosen@1234!")).AccessToken!).ShouldBe(tenantId);
    }

    [Fact]
    public async Task A_Revoked_Invitation_No_Longer_Works()
    {
        var tenantId = await CreateTenantAsync("revoke");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"revoke-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var token = await TestUsers.WaitForInvitationTokenAsync(email);

        var list = await admin.GetFromJsonAsync<PagedResult<InvitationRow>>($"{TestConstants.IdentityBasePath}/invitations", JsonOptions);
        var invitation = list!.Items.Single(i => string.Equals(i.Email, email, StringComparison.OrdinalIgnoreCase));
        invitation.Status.ShouldBe("Pending");
        var userId = await TestUsers.FindUserIdAsync(_factory, email);
        (await admin.DeleteAsync($"{TestConstants.IdentityBasePath}/invitations/{invitation.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Chosen@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
        await WaitForAuditAsync("InvitationRevoked", userId);

        // The account made for the invitation was never claimed, so it went with it: the address is free again.
        using var scope = _factory.Services.CreateScope();
        var normalized = email.ToUpperInvariant();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == normalized)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_New_Invitation_Replaces_The_Previous_One()
    {
        var tenantId = await CreateTenantAsync("reinvite");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"reinvite-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var first = await TestUsers.WaitForInvitationTokenAsync(email);
        await TestUsers.InviteAsync(admin, email);
        var second = await TestUsers.WaitForInvitationTokenAsync(email, previousCount: 1);

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token = first, password = "Chosen@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest, "the earlier link stops working");
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token = second, password = "Chosen@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Deactivated_Tenant_Takes_No_New_Members()
    {
        var tenantId = await CreateTenantAsync("closedten");
        using var admin = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantId), TestConstants.DefaultPassword, tenantId);
        var email = $"closed-{Guid.NewGuid():N}@example.com";
        await TestUsers.InviteAsync(admin, email);
        var token = await TestUsers.WaitForInvitationTokenAsync(email);

        using var root = await _auth.CreateRootAdminClientAsync();
        (await root.PostAsJsonAsync($"{TestConstants.TenantsBasePath}/{tenantId}/activation", new { tenantId, isActive = false })).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Chosen@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Someone_Who_Signed_Up_With_Another_Persons_Address_Loses_The_Account_When_The_Owner_Accepts()
    {
        // The attacker signs up with the victim's address and a password of their own; the address is never confirmed.
        var tenantX = await CreateTenantAsync("hijackx");
        var victim = $"victim-{Guid.NewGuid():N}@example.com";
        using (var anonymousSignUp = _factory.CreateClient())
        {
            anonymousSignUp.DefaultRequestHeaders.Add("tenant", tenantX);
            (await anonymousSignUp.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/self-register", new
            {
                firstName = "Mallory",
                lastName = "X",
                email = victim,
                userName = $"mal-{Guid.NewGuid():N}"[..16],
                password = "Attacker@1234!",
                confirmPassword = "Attacker@1234!",
            })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        // A real tenant invites the victim; the link asks the mailbox owner for a password, not a sign-in.
        var tenantY = await CreateTenantAsync("hijacky");
        using var adminY = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantY), TestConstants.DefaultPassword, tenantY);
        await TestUsers.InviteAsync(adminY, victim);
        var token = await TestUsers.WaitForInvitationTokenAsync(victim);
        using var anonymous = _factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/invitations/accept", new { token, password = "Victim@1234!" })).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        (await IssueAsync(victim, "Attacker@1234!")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "the attacker's password is gone");
        (await SignInAsync(victim, "Victim@1234!", tenantY)).AccessToken.ShouldNotBeNull();
        (await IssueAsync(victim, "Victim@1234!", tenantX)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "the attacker's own sign-up does not stay with the owner's account");
    }

    [Fact]
    public async Task Removing_A_User_Ends_Their_Tenant_Access_And_Keeps_The_Others()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("remove");
        var inA = await SignInAsync(user.Email, user.Password, tenantA);
        var inB = await SignInAsync(user.Email, user.Password, tenantB);
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);

        (await adminA.DeleteAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshAsync(inA.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(inB.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await adminA.GetAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await IssueAsync(user.Email, user.Password, tenantA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await WaitForAuditAsync("MembershipRemoved", user.UserId);
    }

    [Fact]
    public async Task A_Password_Reset_Ends_Every_Session_And_The_Lockout()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("resetend");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        await SetLockoutAsync(user.UserId, DateTimeOffset.UtcNow.AddMinutes(15));

        string resetToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
            var account = (await users.FindByEmailAsync(user.Email))!;
            resetToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(account)));
        }

        // The password belongs to the account, so the reset needs no tenant.
        using var anonymous = _factory.CreateClient();
        using (var reset = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = user.Email, password = "Reset@1234!", token = resetToken }))
        {
            reset.StatusCode.ShouldBe(HttpStatusCode.OK, await reset.Content.ReadAsStringAsync());
        }

        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SignInAsync(user.Email, "Reset@1234!", tenantA)).AccessToken.ShouldNotBeNull("the reset also ends the lockout");
        await WaitForAuditAsync("PasswordReset", user.UserId);
    }

    [Fact]
    public async Task A_Password_Reset_Refuses_A_Recent_Password_And_Changes_Nothing()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("resetreuse");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        var resetToken = await ResetTokenAsync(user.Email);

        using var anonymous = _factory.CreateClient();
        using var reuse = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = user.Email, password = user.Password, token = resetToken });
        reuse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await reuse.Content.ReadAsStringAsync()).ShouldContain("Validation.PasswordRecentlyUsed");

        // Nothing changed: the session still refreshes, and the same token still works once.
        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var fresh = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = user.Email, password = "Fresh@1234!", token = resetToken });
        fresh.StatusCode.ShouldBe(HttpStatusCode.OK, await fresh.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_Password_Reset_With_A_Bad_Token_Never_Says_Whether_The_Password_Was_Used()
    {
        var (_, _, user) = await CreateUserInTwoTenantsAsync("resetleak");
        var badToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-token"));

        using var anonymous = _factory.CreateClient();
        using var reused = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = user.Email, password = user.Password, token = badToken });
        using var unknown = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/reset-password", new { email = $"nobody-{Guid.NewGuid():N}@example.com", password = user.Password, token = badToken });

        reused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var reusedBody = await reused.Content.ReadAsStringAsync();
        reusedBody.ShouldContain("Identity.ErrorResettingPassword");
        reusedBody.ShouldNotContain("PasswordRecentlyUsed");
        (await unknown.Content.ReadAsStringAsync()).ShouldContain("Identity.ErrorResettingPassword");
    }

    private async Task<string> ResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
        var account = (await users.FindByEmailAsync(email))!;
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(account)));
    }

    [Fact]
    public async Task Confirm_Email_Fails_The_Same_Way_Whatever_Went_Wrong()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("confirmenum");
        using var anonymous = _factory.CreateClient();
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("wrong"));

        anonymous.DefaultRequestHeaders.Add("tenant", tenantA);
        using var known = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/confirm-email", new { userId = user.UserId, code, password = user.Password });
        using var stranger = await anonymous.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/confirm-email", new { userId = Guid.NewGuid().ToString(), code, password = user.Password });

        known.StatusCode.ShouldBe(stranger.StatusCode);
        var knownBody = await known.Content.ReadAsStringAsync();
        knownBody.ShouldNotContain(user.Email, Case.Insensitive);
        knownBody.ShouldContain("Identity.EmailConfirmationError");
        (await stranger.Content.ReadAsStringAsync()).ShouldContain("Identity.EmailConfirmationError");
    }

    [Fact]
    public async Task There_Is_No_Way_For_An_Administrator_To_Set_A_Password_Or_Confirm_An_Email()
    {
        using var root = await _auth.CreateRootAdminClientAsync();

        using var register = await root.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/register", new
        {
            firstName = "A",
            lastName = "B",
            email = $"noreg-{Guid.NewGuid():N}@example.com",
            userName = "noreg",
            password = "Test@1234!",
            confirmPassword = "Test@1234!",
        });
        ((int)register.StatusCode is 404 or 405).ShouldBeTrue("the admin register endpoint is gone");

        using var confirm = await root.PostAsync($"{TestConstants.IdentityBasePath}/users/{Guid.NewGuid()}/confirm-email", null);
        ((int)confirm.StatusCode is 404 or 405).ShouldBeTrue("the admin confirm-email endpoint is gone");
    }

    // ---- Selection token ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_Selection_Token_Works_Once()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("seluse");
        var chooser = await SignInAsync(user.Email, user.Password);
        chooser.TenantSelectionRequired.ShouldBeTrue();

        var body = new { selectionToken = chooser.SelectionToken, tenant = tenantA };
        using var client = _factory.CreateClient();
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/select", body)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/select", new { selectionToken = chooser.SelectionToken, tenant = tenantB }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Password_Change_Cancels_A_Pending_Selection()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("selpwd");
        var chooser = await SignInAsync(user.Email, user.Password);
        chooser.TenantSelectionRequired.ShouldBeTrue();

        await ChangePasswordAsync(user, tenantA, "Changed@1234!");

        using var client = _factory.CreateClient();
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/select", new { selectionToken = chooser.SelectionToken, tenant = tenantA }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Select_Keeps_The_App_Boundary()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("selapp");
        var chooser = await SignInAsync(user.Email, user.Password);

        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/select")
        {
            Content = JsonContent.Create(new { selectionToken = chooser.SelectionToken, tenant = tenantA }),
        };
        request.Headers.Add(AppHeader, "admin");

        (await client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Refresh -----------------------------------------------------------------------------------------

    [Fact]
    public async Task Reusing_A_Rotated_Refresh_Token_Ends_The_Session()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("reuse");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);

        var rotated = await RefreshAsync(signedIn.RefreshToken!);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var next = (await rotated.Content.ReadFromJsonAsync<TokenRefreshResult>(JsonOptions))!;

        // The old token comes back (a stolen copy, say): the whole session ends, the new token with it.
        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(next.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Stops_After_The_Membership_Is_Deactivated()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("refdeact");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);

        using var deactivate = new HttpRequestMessage(HttpMethod.Patch, $"{TestConstants.IdentityBasePath}/users/{user.UserId}")
        {
            Content = JsonContent.Create(new { activateUser = false }),
        };
        (await adminA.SendAsync(deactivate)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await WaitForAuditAsync("MembershipDeactivated", user.UserId);
    }

    [Fact]
    public async Task Refresh_Stops_After_The_Tenant_Is_Deactivated()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("reftenant");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);

        using var root = await _auth.CreateRootAdminClientAsync();
        using var deactivate = await root.PostAsJsonAsync($"{TestConstants.TenantsBasePath}/{tenantA}/activation", new { tenantId = tenantA, isActive = false });
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK, await deactivate.Content.ReadAsStringAsync());

        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Password_Change_Ends_Every_Session()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("refpwd");
        var inA = await SignInAsync(user.Email, user.Password, tenantA);
        var inB = await SignInAsync(user.Email, user.Password, tenantB);

        await ChangePasswordAsync(user, tenantA, "Changed@1234!");

        (await RefreshAsync(inA.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(inB.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_Stops_While_The_Account_Is_Locked_Out()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("reflock");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        await SetLockoutAsync(user.UserId, DateTimeOffset.UtcNow.AddMinutes(15));

        (await RefreshAsync(signedIn.RefreshToken!)).StatusCode.ShouldBe((HttpStatusCode)423);
    }

    [Fact]
    public async Task Refresh_Keeps_The_App_Boundary()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("refapp");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);

        (await RefreshAsync(signedIn.RefreshToken!, app: "admin")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Switch ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Switch_Sets_The_Default_And_Is_Audited()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swdef");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        using var client = Bearer(signedIn.AccessToken!);

        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB, setDefault = true });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        TenantClaim((await SignInAsync(user.Email, user.Password)).AccessToken!).ShouldBe(tenantB);
        using var adminB = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantB), TestConstants.DefaultPassword, tenantB);
        await WaitForAuditAsync("TenantSwitched", user.UserId);
    }

    [Fact]
    public async Task Switch_Keeps_The_App_Boundary()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swapp");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        using var client = Bearer(signedIn.AccessToken!);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/switch")
        {
            Content = JsonContent.Create(new { tenant = tenantB }),
        };
        request.Headers.Add(AppHeader, "admin");

        (await client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Switch_Refuses_A_Token_From_Before_A_Password_Change()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swpwd");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        await ChangePasswordAsync(user, tenantA, "Changed@1234!");

        using var client = Bearer(signedIn.AccessToken!);
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB })).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Switch_Refuses_A_Locked_Out_Account()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("swlock");
        var signedIn = await SignInAsync(user.Email, user.Password, tenantA);
        await SetLockoutAsync(user.UserId, DateTimeOffset.UtcNow.AddMinutes(15));

        using var client = Bearer(signedIn.AccessToken!);
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB })).StatusCode
            .ShouldBe((HttpStatusCode)423);
    }

    [Fact]
    public async Task Switch_Is_Refused_While_Impersonating()
    {
        var (_, tenantB, user) = await CreateUserInTwoTenantsAsync("swimp");
        using var root = await _auth.CreateRootAdminClientAsync();
        using var start = await root.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/impersonation/start", new
        {
            targetUserId = user.UserId,
            targetTenantId = tenantB,
            reason = "Support ticket for the switch test",
        });
        start.StatusCode.ShouldBe(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        var impersonation = await start.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = impersonation.GetProperty("accessToken").GetString()!;

        using var client = Bearer(accessToken);
        (await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/token/switch", new { tenant = tenantB })).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Permissions per tenant --------------------------------------------------------------------------

    [Fact]
    public async Task Permissions_Belong_To_The_Tenant_Of_The_Token()
    {
        var (tenantA, tenantB, user) = await CreateUserInTwoTenantsAsync("perm");
        using var adminA = await _auth.CreateAuthenticatedClientAsync(AdminEmail(tenantA), TestConstants.DefaultPassword, tenantA);
        using var grant = await adminA.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/users/{user.UserId}/roles", new
        {
            userId = user.UserId,
            userRoles = new[] { new { roleName = "Admin", enabled = true } },
        });
        grant.IsSuccessStatusCode.ShouldBeTrue(await grant.Content.ReadAsStringAsync());

        using var inA = Bearer((await SignInAsync(user.Email, user.Password, tenantA)).AccessToken!);
        using var inB = Bearer((await SignInAsync(user.Email, user.Password, tenantB)).AccessToken!);
        var permissionsA = await inA.GetStringAsync($"{TestConstants.IdentityBasePath}/permissions");
        var permissionsB = await inB.GetStringAsync($"{TestConstants.IdentityBasePath}/permissions");

        permissionsA.ShouldContain("Permissions.Users.View");
        permissionsB.ShouldNotContain("Permissions.Users.View");
        await WaitForAuditAsync("RoleAssigned", user.UserId);
    }

    // ---- Sign-in enumeration -----------------------------------------------------------------------------

    [Fact]
    public async Task A_Locked_Account_Answers_Like_A_Wrong_Password_Unless_The_Password_Is_Right()
    {
        var (tenantA, _, user) = await CreateUserInTwoTenantsAsync("lockenum");
        await SetLockoutAsync(user.UserId, DateTimeOffset.UtcNow.AddMinutes(15));

        (await IssueAsync(user.Email, "Wrong@1234!", tenantA)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await IssueAsync($"nobody-{Guid.NewGuid():N}@example.com", "Wrong@1234!")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await IssueAsync(user.Email, user.Password, tenantA)).StatusCode.ShouldBe((HttpStatusCode)423);
    }

    // ---- Helpers -----------------------------------------------------------------------------------------

    private const string AppHeader = "X-Trislab-App";

    private async Task<HttpResponseMessage> RefreshAsync(string refreshToken, string? app = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{TestConstants.IdentityBasePath}/token/refresh")
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        if (app is not null)
        {
            request.Headers.Add(AppHeader, app);
        }

        return await client.SendAsync(request);
    }

    private async Task ChangePasswordAsync(InvitedUser user, string tenant, string newPassword)
    {
        using var client = Bearer((await SignInAsync(user.Email, user.Password, tenant)).AccessToken!);
        using var response = await client.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/change-password", new
        {
            password = user.Password,
            newPassword,
            confirmNewPassword = newPassword,
        });
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task SetLockoutAsync(string userId, DateTimeOffset until)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Users.IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnabled, true).SetProperty(u => u.LockoutEnd, until));
    }

    // Audit records are written in the background: poll the audit store for the event about this subject.
    private async Task WaitForAuditAsync(string reasonCode, string subjectId)
    {
        for (var attempt = 0; attempt < 75; attempt++)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var payloads = await db.AuditRecords.IgnoreQueryFilters().AsNoTracking()
                .Where(a => a.Source == "Identity" && a.OccurredAtUtc > DateTime.UtcNow.AddMinutes(-30))
                .Select(a => a.PayloadJson)
                .ToListAsync();
            if (payloads.Any(p => p != null && p.Contains(reasonCode, StringComparison.Ordinal) && p.Contains(subjectId, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new ShouldAssertException($"No audit record '{reasonCode}' for {subjectId}.");
    }

    private static List<string> PropertyNames(string json) =>
        JsonDocument.Parse(json).RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

    private sealed record InvitationRow(Guid Id, string Email, string Status);
}
