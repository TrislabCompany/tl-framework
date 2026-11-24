using Amazon.S3;
using Amazon.S3.Model;
using Trislab.Framework.Storage;
using Trislab.Framework.Storage.S3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Framework.Tests.Storage;

/// <summary>
/// The storage checks of /api/health/details: they must report a store that does not answer, which the
/// storage service's own calls hide (they turn every error into "not found").
/// </summary>
public sealed class StorageHealthCheckTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tl-storage-health-tests", Guid.NewGuid().ToString("N"));

    private static HealthCheckContext Context(IHealthCheck check) => new()
    {
        Registration = new HealthCheckRegistration("storage", check, HealthStatus.Degraded, tags: null),
    };

    private static IOptions<S3StorageOptions> S3Options() =>
        Options.Create(new S3StorageOptions { Bucket = "files", Prefix = "app" });

    [Fact]
    public async Task S3_Should_Be_Healthy_When_The_Bucket_Answers()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns(new ListObjectsV2Response());
        var check = new S3StorageHealthCheck(s3, S3Options());

        var result = await check.CheckHealthAsync(Context(check));

        result.Status.ShouldBe(HealthStatus.Healthy);
        await s3.Received(1).ListObjectsV2Async(
            Arg.Is<ListObjectsV2Request>(r => r.BucketName == "files" && r.Prefix == "app/" && r.MaxKeys == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task S3_Should_Be_Degraded_When_The_Bucket_Does_Not_Answer()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns<ListObjectsV2Response>(_ => throw new AmazonS3Exception("connection refused"));
        var check = new S3StorageHealthCheck(s3, S3Options());

        var result = await check.CheckHealthAsync(Context(check));

        result.Status.ShouldBe(HealthStatus.Degraded);
        result.Exception.ShouldBeOfType<AmazonS3Exception>();
    }

    [Fact]
    public async Task Local_Should_Be_Healthy_When_The_Folder_Takes_A_File_And_Leave_Nothing_Behind()
    {
        Directory.CreateDirectory(_root);
        var check = new LocalStorageHealthCheck(Environment(_root));

        var result = await check.CheckHealthAsync(Context(check));

        result.Status.ShouldBe(HealthStatus.Healthy);
        Directory.EnumerateFileSystemEntries(_root).ShouldBeEmpty();
    }

    [Fact]
    public async Task Local_Should_Be_Degraded_When_The_Folder_Is_Missing()
    {
        var check = new LocalStorageHealthCheck(Environment(_root));

        var result = await check.CheckHealthAsync(Context(check));

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    private static IWebHostEnvironment Environment(string root)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.WebRootPath.Returns(root);
        environment.ContentRootPath.Returns(root);
        return environment;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
