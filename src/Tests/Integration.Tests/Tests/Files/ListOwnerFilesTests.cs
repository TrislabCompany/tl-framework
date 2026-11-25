using System.Security.Cryptography;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Trislab.Framework.Shared.Multitenancy;
using Trislab.Modules.Files.Contracts.v1.DTOs;
using Trislab.Modules.Files.Data;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using TestHost;

namespace Integration.Tests.Tests.Files;

/// <summary>
/// <c>GET /files?ownerType=&amp;ownerId=</c>: the files of one owner, through its owner type's policy. Runs in both
/// tenancy modes: see <see cref="ListOwnerFilesTests"/> and <see cref="ListOwnerFilesTestsInSingleTenantMode"/>. The
/// cases with a second user are in <see cref="ListOwnerFilesAcrossUsersTests"/>, multi-tenant only, as the other
/// Files tests with a second user are. The <c>TestItem</c> policy of the test host lets every signed-in user read,
/// and refuses the list of <see cref="TestItemFileAccessPolicy.UnlistableOwnerId"/>.
/// </summary>
public abstract class ListOwnerFilesTestsBase
{
    private const string TestItem = TestItemFileAccessPolicy.OwnerTypeName;

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    protected ListOwnerFilesTestsBase(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Lists_only_the_owners_files_newest_first()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var first = await OwnerFiles.UploadAsync(client, TestItem, owner, "first.pdf", visibility: 0);
        var second = await OwnerFiles.UploadAsync(client, TestItem, owner, "second.pdf", visibility: 1);
        await OwnerFiles.UploadAsync(client, TestItem, Guid.NewGuid(), "elsewhere.pdf", visibility: 0);

        var page = await OwnerFiles.ListAsync(client, TestItem, owner);

        page.Items.Select(f => f.Id).ShouldBe([second.Id, first.Id]);
        page.TotalCount.ShouldBe(2);
        page.Items.ShouldAllBe(f => f.OwnerId == owner && f.OwnerType == TestItem);
        page.Items.Single(f => f.Id == first.Id).PublicUrl.ShouldNotBeNullOrEmpty();
        page.Items.Single(f => f.Id == second.Id).PublicUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Leaves_out_pending_quarantined_and_deleted_files()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var available = await OwnerFiles.UploadAsync(client, TestItem, owner, "available.pdf", visibility: 0);
        await OwnerFiles.RequestUploadAsync(client, TestItem, owner, "pending.pdf", visibility: 0); // never finalized
        var quarantined = await OwnerFiles.UploadAsync(client, TestItem, owner, "quarantined.pdf", visibility: 0);
        await QuarantineAsync(quarantined.Id);
        var deleted = await OwnerFiles.UploadAsync(client, TestItem, owner, "deleted.pdf", visibility: 0);
        using (var delete = await client.DeleteAsync($"{OwnerFiles.BasePath}/{deleted.Id}"))
        {
            delete.IsSuccessStatusCode.ShouldBeTrue();
        }

        var page = await OwnerFiles.ListAsync(client, TestItem, owner);

        page.Items.Select(f => f.Id).ShouldBe([available.Id]);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Matches_the_owner_type_whatever_its_case()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var file = await OwnerFiles.UploadAsync(client, "TESTITEM", owner, "shouted.pdf", visibility: 0);

        var page = await OwnerFiles.ListAsync(client, "testitem", owner);

        page.Items.Select(f => f.Id).ShouldBe([file.Id]);
        page.Items.Single().OwnerType.ShouldBe(TestItem);
    }

    [Fact]
    public async Task Pages_the_files_the_caller_may_read()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var oldest = await OwnerFiles.UploadAsync(client, TestItem, owner, "a.pdf", visibility: 0);
        var middle = await OwnerFiles.UploadAsync(client, TestItem, owner, "b.pdf", visibility: 0);
        var newest = await OwnerFiles.UploadAsync(client, TestItem, owner, "c.pdf", visibility: 0);

        var first = await OwnerFiles.ListAsync(client, TestItem, owner, pageNumber: 1, pageSize: 2);
        var second = await OwnerFiles.ListAsync(client, TestItem, owner, pageNumber: 2, pageSize: 2);

        first.Items.Select(f => f.Id).ShouldBe([newest.Id, middle.Id]);
        second.Items.Select(f => f.Id).ShouldBe([oldest.Id]);
        first.TotalCount.ShouldBe(3);
        first.TotalPages.ShouldBe(2);
    }

    [Fact]
    public async Task Lists_the_users_own_MyFiles_and_refuses_another_users()
    {
        using var client = await _auth.CreateRootAdminClientAsync();
        var probe = await OwnerFiles.UploadAsync(client, TestItem, Guid.NewGuid(), "probe.pdf", visibility: 0);
        var me = Guid.Parse(probe.CreatedByUserId);
        var mine = await OwnerFiles.UploadAsync(client, "MyFiles", me, "mine.pdf", visibility: 1);

        var page = await OwnerFiles.ListAsync(client, "MyFiles", me);
        using var someoneElses = await client.GetAsync($"{OwnerFiles.BasePath}?ownerType=MyFiles&ownerId={Guid.NewGuid()}");

        page.Items.Select(f => f.Id).ShouldBe([mine.Id]);
        someoneElses.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refuses_an_owner_type_without_a_policy()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync($"{OwnerFiles.BasePath}?ownerType=NoSuchOwner&ownerId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refuses_a_list_the_policy_denies()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync(
            $"{OwnerFiles.BasePath}?ownerType={TestItem}&ownerId={TestItemFileAccessPolicy.UnlistableOwnerId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_an_empty_owner_id()
    {
        using var client = await _auth.CreateRootAdminClientAsync();

        using var response = await client.GetAsync($"{OwnerFiles.BasePath}?ownerType={TestItem}&ownerId={Guid.Empty}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Needs_a_signed_in_user()
    {
        using var anonymous = _factory.CreateClient();

        using var response = await anonymous.GetAsync($"{OwnerFiles.BasePath}?ownerType={TestItem}&ownerId={Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>The scanner of the test host passes every file, so a quarantined file is made in the database.</summary>
    private async Task QuarantineAsync(Guid fileId)
    {
        using var scope = _factory.Services.CreateScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>().GetAsync(_factory.TenantId);
        tenant.ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>().MultiTenantContext =
            new MultiTenantContext<AppTenantInfo>(tenant);
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        var updated = await db.FileAssets
            .Where(f => f.Id == fileId)
            .ExecuteUpdateAsync(set => set.SetProperty(f => f.Status, FileAssetStatus.Quarantined));
        updated.ShouldBe(1);
    }
}

[Collection(TrislabCollectionDefinition.Name)]
public sealed class ListOwnerFilesTests(TrislabWebApplicationFactory factory) : ListOwnerFilesTestsBase(factory);

[Collection(SingleTenantCollectionDefinition.Name)]
public sealed class ListOwnerFilesTestsInSingleTenantMode(SingleTenantWebApplicationFactory factory) : ListOwnerFilesTestsBase(factory);

/// <summary>
/// The owner list seen by a second user of the same tenant, through <see cref="TestDocumentFileAccessPolicy"/>
/// (a private file only for its uploader) and the built-in <c>MyFiles</c> policy (only the user's own id).
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class ListOwnerFilesAcrossUsersTests
{
    private const string TestDocument = TestDocumentFileAccessPolicy.OwnerTypeName;

    private readonly TrislabWebApplicationFactory _factory;
    private readonly AuthHelper _auth;

    public ListOwnerFilesAcrossUsersTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
        _auth = new AuthHelper(factory);
    }

    [Fact]
    public async Task Hides_the_private_files_the_policy_does_not_let_the_caller_read_and_counts_only_the_rest()
    {
        using var uploader = await _auth.CreateRootAdminClientAsync();
        var owner = Guid.NewGuid();
        var olderShared = await OwnerFiles.UploadAsync(uploader, TestDocument, owner, "older.pdf", visibility: 0);
        var mine = await OwnerFiles.UploadAsync(uploader, TestDocument, owner, "mine.pdf", visibility: 1);
        var newerShared = await OwnerFiles.UploadAsync(uploader, TestDocument, owner, "newer.pdf", visibility: 0);
        using var colleague = await RegisterAndLoginAsync("owner-list");

        var colleagueFirst = await OwnerFiles.ListAsync(colleague, TestDocument, owner, pageNumber: 1, pageSize: 1);
        var colleagueSecond = await OwnerFiles.ListAsync(colleague, TestDocument, owner, pageNumber: 2, pageSize: 1);
        var uploaders = await OwnerFiles.ListAsync(uploader, TestDocument, owner);

        colleagueFirst.Items.Select(f => f.Id).ShouldBe([newerShared.Id]);
        colleagueSecond.Items.Select(f => f.Id).ShouldBe([olderShared.Id]);
        colleagueFirst.TotalCount.ShouldBe(2);
        colleagueFirst.TotalPages.ShouldBe(2);
        uploaders.Items.Select(f => f.Id).ShouldBe([newerShared.Id, mine.Id, olderShared.Id]);
        uploaders.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task Refuses_the_MyFiles_of_another_user()
    {
        using var colleague = await RegisterAndLoginAsync("myfiles-owner");
        var colleaguesFile = await OwnerFiles.UploadAsync(colleague, TestDocument, Guid.NewGuid(), "probe.pdf", visibility: 0);
        var colleagueId = Guid.Parse(colleaguesFile.CreatedByUserId);
        await OwnerFiles.UploadAsync(colleague, "MyFiles", colleagueId, "theirs.pdf", visibility: 0);
        using var admin = await _auth.CreateRootAdminClientAsync();

        using var response = await admin.GetAsync($"{OwnerFiles.BasePath}?ownerType=MyFiles&ownerId={colleagueId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> RegisterAndLoginAsync(string prefix)
    {
        using var adminClient = await _auth.CreateRootAdminClientAsync();
        var user = await TestUsers.InviteAndAcceptAsync(_factory, adminClient, prefix, prefix, "Test");
        return await _auth.CreateAuthenticatedClientAsync(user.Email, user.Password);
    }
}

/// <summary>The upload and list calls of the owner-list tests.</summary>
internal static class OwnerFiles
{
    public const string BasePath = "/api/v1/files";

    public static async Task<PagedResponse<FileAssetDto>> ListAsync(
        HttpClient client, string ownerType, Guid ownerId, int pageNumber = 1, int pageSize = 20)
    {
        using var response = await client.GetAsync(
            $"{BasePath}?ownerType={ownerType}&ownerId={ownerId}&pageNumber={pageNumber}&pageSize={pageSize}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.DeserializeAsync<PagedResponse<FileAssetDto>>();
    }

    /// <summary>Asks for an upload URL only, which leaves the file pending.</summary>
    public static async Task<PresignedUploadResponse> RequestUploadAsync(
        HttpClient client, string ownerType, Guid ownerId, string fileName, int visibility, int sizeBytes = 128)
    {
        using var urlResponse = await client.PostAsJsonAsync($"{BasePath}/upload-url", new
        {
            ownerType,
            ownerId,
            fileName,
            contentType = "application/pdf",
            sizeBytes,
            visibility,
            category = "Document",
        });
        urlResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await urlResponse.DeserializeAsync<PresignedUploadResponse>();
    }

    /// <summary>Uploads and finalizes a file, and returns it as the API does.</summary>
    public static async Task<FileAssetDto> UploadAsync(HttpClient client, string ownerType, Guid ownerId, string fileName, int visibility)
    {
        byte[] bytes = new byte[128];
        RandomNumberGenerator.Fill(bytes);
        var presigned = await RequestUploadAsync(client, ownerType, ownerId, fileName, visibility, bytes.Length);

        using var raw = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, presigned.UploadUrl)
        {
            Content = new ByteArrayContent(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") } },
        };
        using var putResponse = await raw.SendAsync(put);
        putResponse.EnsureSuccessStatusCode();

        using var finalize = await client.PostAsync($"{BasePath}/{presigned.FileAssetId}/finalize", null);
        finalize.EnsureSuccessStatusCode();
        return await finalize.DeserializeAsync<FileAssetDto>();
    }
}
