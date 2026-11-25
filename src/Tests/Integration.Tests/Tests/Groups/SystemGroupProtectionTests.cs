using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;

namespace Integration.Tests.Tests.Groups;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class SystemGroupProtectionTests
{
    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public SystemGroupProtectionTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task UpdateGroup_Should_ReturnForbidden_When_GroupIsSystemGroup()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var systemGroup = await GetGroupByNameAsync(client, "All Users");

        var response = await client.PutAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/groups/{systemGroup.Id}",
            new
            {
                name = "Renamed All Users",
                description = "Attempt to rename a seeded system group.",
                isDefault = systemGroup.IsDefault,
                roleIds = new List<string>()
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateGroup_Should_ReturnForbidden_When_GroupIsAdministrators()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var administratorsGroup = await GetGroupByNameAsync(client, "Administrators");

        var response = await client.PutAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/groups/{administratorsGroup.Id}",
            new
            {
                name = "Renamed Administrators",
                description = "Attempt to rename the Administrators group.",
                isDefault = administratorsGroup.IsDefault,
                roleIds = new List<string>()
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteGroup_Should_ReturnForbidden_When_GroupIsSystemGroup()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var systemGroup = await GetGroupByNameAsync(client, "All Users");

        var response = await client.DeleteAsync(
            $"{TestConstants.IdentityBasePath}/groups/{systemGroup.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveUserFromGroup_Should_ReturnForbidden_When_GroupIsDefault()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var allUsersGroup = await GetGroupByNameAsync(client, "All Users");

        // Invite a fresh user — accepting the invitation auto-assigns them to every default group.
        var registered = await TestUsers.InviteAndAcceptAsync(_factory, client, "defaultgroup", "DefaultGroup", "Member");

        var response = await client.DeleteAsync(
            $"{TestConstants.IdentityBasePath}/groups/{allUsersGroup.Id}/members/{registered.UserId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<GroupDto> GetGroupByNameAsync(HttpClient client, string name)
    {
        var response = await client.GetAsync($"{TestConstants.IdentityBasePath}/groups");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var groups = await response.DeserializeAsync<GroupDto[]>();
        return groups.First(g => g.Name == name);
    }
}
