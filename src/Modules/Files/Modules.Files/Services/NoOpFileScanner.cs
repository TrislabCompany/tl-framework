using Trislab.Modules.Files.Domain;

namespace Trislab.Modules.Files.Services;

internal sealed class NoOpFileScanner : IFileScanner
{
    public ValueTask<ScanStatus> ScanAsync(string storageKey, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ScanStatus.Clean);
}
