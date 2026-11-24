using Trislab.Modules.Files.Authorization;
using Trislab.Modules.Files.Contracts;

namespace Files.Tests.Authorization;

public class DefaultUploaderOnlyPolicyTests
{
    private static FileAccessContext PublicFileOwnedBy(string uploaderId) =>
        new(Guid.NewGuid(), "MyFiles", null, uploaderId, Visibility: 0);

    private static FileAccessContext PrivateFileOwnedBy(string uploaderId) =>
        new(Guid.NewGuid(), "MyFiles", null, uploaderId, Visibility: 1);

    [Fact]
    public async Task CanAttachAsync_Should_AllowAuthenticated()
    {
        var p = new DefaultUploaderOnlyPolicy("MyFiles");
        (await p.CanAttachAsync(null, "user-1", default)).ShouldBeTrue();
    }

    [Fact]
    public async Task CanAttachAsync_Should_DenyAnonymous()
    {
        var p = new DefaultUploaderOnlyPolicy("MyFiles");
        (await p.CanAttachAsync(null, "", default)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanListAsync_Should_AllowOnly_TheCallersOwnUserId()
    {
        IFileAccessPolicy p = new DefaultUploaderOnlyPolicy("MyFiles");
        var me = Guid.NewGuid();
        (await p.CanListAsync(me, me.ToString(), default)).ShouldBeTrue();
        (await p.CanListAsync(Guid.NewGuid(), me.ToString(), default)).ShouldBeFalse();
        (await p.CanListAsync(null, me.ToString(), default)).ShouldBeFalse();
        (await p.CanListAsync(me, "", default)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanListAsync_Should_FollowTheAttachRule_WhenAPolicyDoesNotOverrideIt()
    {
        IFileAccessPolicy p = new AttachOnlyPolicy();
        (await p.CanListAsync(Guid.NewGuid(), "user-1", default)).ShouldBeTrue();
        (await p.CanListAsync(Guid.NewGuid(), "", default)).ShouldBeFalse();
    }

    private sealed class AttachOnlyPolicy : IFileAccessPolicy
    {
        public string OwnerType => "Thing";

        public Task<bool> CanAttachAsync(Guid? ownerId, string currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(!string.IsNullOrEmpty(currentUserId));

        public Task<bool> CanReadAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task<bool> CanDeleteAsync(FileAccessContext context, string currentUserId, CancellationToken cancellationToken)
            => Task.FromResult(false);
    }

    [Fact]
    public async Task CanReadAsync_Should_AllowAnyone_ForPublicFile()
    {
        var p = new DefaultUploaderOnlyPolicy("MyFiles");
        (await p.CanReadAsync(PublicFileOwnedBy("uploader"), "someone-else", default)).ShouldBeTrue();
    }

    [Fact]
    public async Task CanReadAsync_Should_AllowUploaderOnly_ForPrivateFile()
    {
        var p = new DefaultUploaderOnlyPolicy("MyFiles");
        (await p.CanReadAsync(PrivateFileOwnedBy("uploader"), "uploader", default)).ShouldBeTrue();
        (await p.CanReadAsync(PrivateFileOwnedBy("uploader"), "someone-else", default)).ShouldBeFalse();
    }

    [Fact]
    public async Task CanDeleteAsync_Should_AllowUploaderOnly()
    {
        var p = new DefaultUploaderOnlyPolicy("MyFiles");
        (await p.CanDeleteAsync(PublicFileOwnedBy("uploader"), "uploader", default)).ShouldBeTrue();
        (await p.CanDeleteAsync(PublicFileOwnedBy("uploader"), "someone-else", default)).ShouldBeFalse();
    }
}
