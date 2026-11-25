using System.Text.Json;
using Trislab.Modules.Auditing.Contracts.Dtos;
using Trislab.Modules.Identity.Contracts.DTOs;
using Integration.Tests.Infrastructure;
using Integration.Tests.Tests.Sessions;

namespace Integration.Tests.Tests.Security;

/// <summary>
/// What a Basic user may read, in both tenancy modes: users only through the picker lookup, audit
/// records only their own, health only the probes' overall status. An Admin keeps the full views.
/// </summary>
public abstract class BasicUserAccessTestsBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    protected BasicUserAccessTestsBase(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    private async Task<(SeededUser User, HttpClient Client)> CreateBasicClientAsync(HttpClient adminClient, string prefix)
    {
        // The first name carries the prefix, so a lookup by name finds the user; the user name is generated.
        var invited = await TestUsers.InviteAndAcceptAsync(_factory, adminClient, prefix, firstName: prefix);
        var user = new SeededUser(invited.UserId, invited.Email, invited.Password);
        var client = await _auth.CreateAuthenticatedClientAsync(user.Email, user.Password, _factory.TenantId);
        return (user, client);
    }

    [Theory]
    [InlineData("/users")]
    [InlineData("/users/search?pageNumber=1&pageSize=10")]
    public async Task UserList_Should_Return403_For_A_Basic_User(string path)
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (_, basicClient) = await CreateBasicClientAsync(adminClient, "list-basic");
        using (basicClient)
        {
            using var response = await basicClient.GetAsync($"{TestConstants.IdentityBasePath}{path}");

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Users.View is an Admin permission");
        }
    }

    [Theory]
    [InlineData(TestConstants.IdentityBasePath + "/users")]
    [InlineData(TestConstants.IdentityBasePath + "/users/search?pageNumber=1&pageSize=10")]
    [InlineData(TestConstants.AuditsBasePath + "?pageNumber=1&pageSize=10")]
    public async Task Admin_Should_Keep_The_Full_Views(string path)
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();

        using var response = await adminClient.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UserLookup_Should_Find_Active_Users_By_Name_Without_Their_Email()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (basicUser, basicClient) = await CreateBasicClientAsync(adminClient, "pick-me");
        using (basicClient)
        {
            using var response = await basicClient.GetAsync($"{TestConstants.IdentityBasePath}/users/lookup?search=pick-me&limit=50");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.ShouldNotContain("@example.com", Case.Insensitive, "the lookup never returns an e-mail address");
            var users = JsonSerializer.Deserialize<List<UserLookupDto>>(body, JsonOptions)!;
            users.ShouldContain(u => u.Id == basicUser.UserId);
        }
    }

    [Fact]
    public async Task UserLookup_Should_Not_Match_An_Email_Address()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (basicUser, basicClient) = await CreateBasicClientAsync(adminClient, "mailonly");
        using (basicClient)
        {
            // The user name is "mailonly-<id>", so search a part of the address that only the e-mail has.
            using var response = await basicClient.GetAsync($"{TestConstants.IdentityBasePath}/users/lookup?search=example.com");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(JsonOptions);
            users!.ShouldNotContain(u => u.Id == basicUser.UserId);
        }
    }

    [Fact]
    public async Task UserLookup_Should_Return_Users_By_Id_And_Skip_Inactive_Ones()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (basicUser, basicClient) = await CreateBasicClientAsync(adminClient, "by-id");
        var inactive = await IdentityUserSeeder.CreateLoginableUserAsync(_factory, adminClient, "by-id-off", _factory.TenantId);
        using var deactivate = new HttpRequestMessage(HttpMethod.Patch, $"{TestConstants.IdentityBasePath}/users/{inactive.UserId}")
        {
            Content = JsonContent.Create(new { activateUser = false }),
        };
        (await adminClient.SendAsync(deactivate)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (basicClient)
        {
            using var response = await basicClient.GetAsync(
                $"{TestConstants.IdentityBasePath}/users/lookup?ids={basicUser.UserId}&ids={inactive.UserId}");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var users = await response.Content.ReadFromJsonAsync<List<UserLookupDto>>(JsonOptions);
            users!.Select(u => u.Id).ShouldBe([basicUser.UserId]);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("?search=a&ids=b")]
    public async Task UserLookup_Should_Return400_Without_Exactly_One_Of_Search_And_Ids(string query)
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();

        using var response = await adminClient.GetAsync($"{TestConstants.IdentityBasePath}/users/lookup{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UserLookup_Should_Return401_When_NotAuthenticated()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/users/lookup?search=a");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Audits_Should_Show_A_Basic_User_Only_Their_Own_Activity()
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (basicUser, basicClient) = await CreateBasicClientAsync(adminClient, "my-activity");
        using (basicClient)
        {
            using (var all = await basicClient.GetAsync($"{TestConstants.AuditsBasePath}?pageNumber=1&pageSize=10"))
            {
                all.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "AuditTrails.View is an Admin permission");
            }

            // Audit writes are asynchronous: poll until the user's own sign-in or requests show up.
            PagedResponse<AuditSummaryDto>? page = null;
            for (int attempt = 0; attempt < 40 && (page is null || page.Items.Count == 0); attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }

                using var mine = await basicClient.GetAsync($"{TestConstants.AuditsBasePath}/mine?pageNumber=1&pageSize=100");
                mine.StatusCode.ShouldBe(HttpStatusCode.OK);
                page = await mine.Content.ReadFromJsonAsync<PagedResponse<AuditSummaryDto>>(JsonOptions);
            }

            page!.Items.ShouldNotBeEmpty();
            page.Items.ShouldAllBe(a => a.UserId == basicUser.UserId);
        }
    }

    [Fact]
    public async Task Readiness_Should_Answer_The_Overall_Status_Only()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"results\":[]");
        body.ShouldNotContain("db:");
    }

    [Fact]
    public async Task HealthDetails_Should_Be_For_Admins_Only()
    {
        using var anonymous = _factory.CreateClient();
        using (var response = await anonymous.GetAsync("/api/health/details"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var (_, basicClient) = await CreateBasicClientAsync(adminClient, "health-basic");
        using (basicClient)
        using (var response = await basicClient.GetAsync("/api/health/details"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "Health.View is an Admin permission");
        }

        using (var response = await adminClient.GetAsync("/api/health/details"))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            body.ShouldContain("\"name\":\"storage\"", Case.Sensitive, "the details run the storage check the probe skips");
            body.ShouldContain("\"name\":\"db:files\"");
        }
    }
}

[Collection(TrislabCollectionDefinition.Name)]
public sealed class BasicUserAccessTests(TrislabWebApplicationFactory factory) : BasicUserAccessTestsBase(factory);

[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class BasicUserAccessTestsInSingleTenantMode(SingleTenantWebApplicationFactory factory) : BasicUserAccessTestsBase(factory);
