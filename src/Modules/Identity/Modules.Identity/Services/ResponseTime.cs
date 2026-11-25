namespace Trislab.Modules.Identity.Services;

/// <summary>Settings of self-registration.</summary>
public sealed class SelfRegistrationOptions
{
    public const string SectionName = "Identity:SelfRegistration";

    /// <summary>
    /// The least time a self-registration or a password-reset request takes, whatever the outcome, so a
    /// caller can't tell from the timing whether an address has an account. One second by default.
    /// </summary>
    public TimeSpan MinimumResponseTime { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>Pads a response to a fixed least duration, so its timing says nothing about which path it took.</summary>
internal static class ResponseTime
{
    public static async Task PadAsync(TimeProvider timeProvider, long startedTimestamp, TimeSpan minimum, CancellationToken ct)
    {
        var remaining = minimum - timeProvider.GetElapsedTime(startedTimestamp);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, timeProvider, ct).ConfigureAwait(false);
        }
    }
}
