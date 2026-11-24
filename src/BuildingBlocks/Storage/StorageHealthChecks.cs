using Amazon.S3;
using Amazon.S3.Model;
using Trislab.Framework.Storage.Local;
using Trislab.Framework.Storage.S3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Trislab.Framework.Storage;

/// <summary>
/// Whether the S3 bucket answers: it lists at most one key under the configured prefix. The service's
/// own calls swallow errors (a missing object and an unreachable store both look like "not found"), so
/// the check talks to the client directly. The credentials need <c>s3:ListBucket</c> on the prefix.
/// </summary>
internal sealed class S3StorageHealthCheck(IAmazonS3 s3, IOptions<S3StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bucket = options.Value.Bucket;
        try
        {
            await s3.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = options.Value.Prefix?.Trim().Trim('/') is { Length: > 0 } prefix ? prefix + "/" : null,
                MaxKeys = 1,
            }, cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy("The storage bucket answers.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The storage bucket does not answer.", ex);
        }
    }
}

/// <summary>
/// Whether the local storage folder (the web root) exists and takes a file: it writes and deletes a
/// small file there.
/// </summary>
internal sealed class LocalStorageHealthCheck(IWebHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var root = LocalStorageService.ResolveRootPath(environment);
            if (!Directory.Exists(root))
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "The storage folder does not exist.");
            }

            var probe = Path.Combine(root, $".storage-health-{Guid.NewGuid():N}");
            await File.WriteAllTextAsync(probe, "ok", cancellationToken).ConfigureAwait(false);
            File.Delete(probe);
            return HealthCheckResult.Healthy("The storage folder takes files.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "The storage folder does not take files.", ex);
        }
    }
}
