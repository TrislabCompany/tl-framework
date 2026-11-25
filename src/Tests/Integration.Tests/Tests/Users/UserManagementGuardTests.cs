using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Users;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class UserManagementGuardTests
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public UserManagementGuardTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task DeleteUser_Should_ReturnBadRequest_When_TargetIsSelf()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var profile = await GetCurrentProfileAsync(client);

        var response = await client.DeleteAsync($"{TestConstants.IdentityBasePath}/users/{profile.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteUser_Should_ReturnBadRequest_When_TargetIsAdmin()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        // Invite a fresh user
        var registered = await TestUsers.InviteAndAcceptAsync(_factory, client, "usr", "Other", "Admin");

        // Promote the new user to Admin
        var (adminRole, basicRole) = await GetSystemRolesAsync(client);
        var assignResponse = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{registered.UserId}/roles",
            new
            {
                userId = registered.UserId,
                userRoles = new[]
                {
                    new { roleId = adminRole.Id, roleName = "Admin", enabled = true },
                    new { roleId = basicRole.Id, roleName = "Basic", enabled = true }
                }
            });
        assignResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Root admin tries to delete the second admin → must be rejected.
        var deleteResponse = await client.DeleteAsync($"{TestConstants.IdentityBasePath}/users/{registered.UserId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignUserRoles_Should_ReturnBadRequest_When_RemovingOwnAdminRole()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var profile = await GetCurrentProfileAsync(client);
        var (adminRole, _) = await GetSystemRolesAsync(client);

        var response = await client.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{profile.Id}/roles",
            new
            {
                userId = profile.Id,
                userRoles = new[]
                {
                    new { roleId = adminRole.Id, roleName = "Admin", enabled = false }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static async Task<UserDto> GetCurrentProfileAsync(HttpClient client)
    {
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/profile");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.DeserializeAsync<UserDto>();
    }

    private static async Task<(RoleDto Admin, RoleDto Basic)> GetSystemRolesAsync(HttpClient client)
    {
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/roles");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.DeserializeAsync<PagedResponse<RoleDto>>();
        return (page.Items.First(r => r.Name == "Admin"), page.Items.First(r => r.Name == "Basic"));
    }
}
