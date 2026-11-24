using System.Security.Cryptography;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Trislab.Modules.Identity.Domain;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using TestHost;

namespace Integration.Tests.Tests.Files;

/// <summary>
/// <c>GET /files?ownerType=&amp;ownerId=</c>: the files of one owner, through its owner type's policy. The
/// <c>TestItem</c> policy of the test host lets every signed-in user read and refuses the list of
/// <see cref="TestItemFileAccessPolicy.UnlistableOwnerId"/>; the built-in <c>MyFiles</c> policy shows a
/// private file only to its uploader.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class ListOwnerFilesTests
{
    private const string FilesBasePath = "/api/v1/files";
    private const string TestItem = TestItemFileAccessPolicy.OwnerTypeName;

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public ListOwnerFilesTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Lists_only_the_owners_files_newest_first()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var first = await UploadAsync(client, TestItem, owner, "first.pdf", visibility: 0);
        var second = await UploadAsync(client, TestItem, owner, "second.pdf", visibility: 1);
        await UploadAsync(client, TestItem, other, "elsewhere.pdf", visibility: 0);

        var page = await ListAsync(client, TestItem, owner);

        page.Items.Select(f => f.Id).ShouldBe([second, first]);
        page.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(f => f.OwnerId == owner && f.OwnerType == TestItem);
        page.Items.Single(f => f.Id == first).PublicUrl.ShouldNotBeNullOrEmpty();
        page.Items.Single(f => f.Id == second).PublicUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Hides_the_private_files_the_policy_does_not_let_the_caller_read()
    {
        using var uploader = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var shared = await UploadAsync(uploader, "MyFiles", owner, "shared.pdf", visibility: 0);
        var mine = await UploadAsync(uploader, "MyFiles", owner, "mine.pdf", visibility: 1);
        using var colleague = await RegisterAndLoginAsync("owner-list");

        var seenByColleague = await ListAsync(colleague, "MyFiles", owner);
        var seenByUploader = await ListAsync(uploader, "MyFiles", owner);

        seenByColleague.Items.Select(f => f.Id).ShouldBe([shared]);
        seenByColleague.TotalCount.ShouldBe(1);
        seenByUploader.Items.Select(f => f.Id).ShouldBe([mine, shared]);
    }

    [Fact]
    public async Task Matches_the_owner_type_whatever_its_case()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var file = await UploadAsync(client, TestItem.ToUpperInvariant(), owner, "shouted.pdf", visibility: 0);

        var page = await ListAsync(client, "testitem", owner);

        page.Items.Select(f => f.Id).ShouldBe([file]);
        page.Items.Single().OwnerType.ShouldBe(TestItem);
    }

    [Fact]
    public async Task Pages_the_files_the_caller_may_read()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var oldest = await UploadAsync(client, TestItem, owner, "a.pdf", visibility: 0);
        var middle = await UploadAsync(client, TestItem, owner, "b.pdf", visibility: 0);
        var newest = await UploadAsync(client, TestItem, owner, "c.pdf", visibility: 0);

        var first = await ListAsync(client, TestItem, owner, pageNumber: 1, pageSize: 2);
        var second = await ListAsync(client, TestItem, owner, pageNumber: 2, pageSize: 2);

        first.Items.Select(f => f.Id).ShouldBe([newest, middle]);
        second.Items.Select(f => f.Id).ShouldBe([oldest]);
        first.TotalCount.ShouldBe(3);
        first.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Refuses_an_owner_type_without_a_policy()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync($"{FilesBasePath}?ownerType=NoSuchOwner&ownerId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refuses_a_list_the_policy_denies()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync(
            $"{FilesBasePath}?ownerType={TestItem}&ownerId={TestItemFileAccessPolicy.UnlistableOwnerId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_an_empty_owner_id()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync($"{FilesBasePath}?ownerType={TestItem}&ownerId={Guid.Empty}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Needs_a_signed_in_user()
    {
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync($"{FilesBasePath}?ownerType={TestItem}&ownerId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<PagedResponse<FileAssetDto>> ListAsync(
        HttpClient client, string ownerType, Guid ownerId, int pageNumber = 1, int pageSize = 20)
    {
        using var response = await client.GetAsync(
            $"{FilesBasePath}?ownerType={ownerType}&ownerId={ownerId}&pageNumber={pageNumber}&pageSize={pageSize}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.DeserializeAsync<PagedResponse<FileAssetDto>>();
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string ownerType, Guid ownerId, string fileName, int visibility)
    {
        const string contentType = "application/pdf";
        byte[] bytes = new byte[128];
        RandomNumberGenerator.Fill(bytes);

        using var urlResponse = await client.PostAsJsonAsync($"{FilesBasePath}/upload-url", new
        {
            ownerType,
            ownerId,
            fileName,
            contentType,
            sizeBytes = bytes.Length,
            visibility,
            category = "Document",
        });
        urlResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var presigned = await urlResponse.DeserializeAsync<PresignedUploadResponse>();

        using var raw = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, presigned.UploadUrl)
        {
            Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue(contentType) } },
        };
        using var putResponse = await raw.SendAsync(put);
        putResponse.EnsureSuccessStatusCode();

        using var finalize = await client.PostAsync($"{FilesBasePath}/{presigned.FileAssetId}/finalize", null);
        finalize.EnsureSuccessStatusCode();
        return presigned.FileAssetId;
    }

    private async Task<HttpClient> RegisterAndLoginAsync(string prefix)
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"{prefix}-{unique}@example.com";
        const string password = "Test@1234!";

        using var response = await adminClient.PostAsJsonAsync($"{TestConstants.IdentityBasePath}/register", new
        {
            firstName = prefix,
            lastName = "Test",
            email,
            userName = $"{prefix}{unique}",
            password,
            confirmPassword = password,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var registered = await response.DeserializeAsync<RegisterResult>();

        // The tenant context is set inline (AsyncLocal) so UserManager's query filter resolves the tenant.
        using (var scope = _factory.Services.CreateScope())
        {
            var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
                .GetAsync(TestConstants.RootTenantId);
            scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
                new MultiTenantContext<AppTenantInfo>(tenant);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TrislabUser>>();
            var user = await userManager.FindByIdAsync(registered.UserId);
            user.ShouldNotBeNull();
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                (await userManager.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            }
        }

        return await _auth.CreateAuthenticatedClientAsync(email, password);
    }
}
