using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trislab.Framework.Web.BotProtection;

/// <summary>The answer of Cloudflare's siteverify for one token.</summary>
public enum TurnstileResult
{
    /// <summary>The token is valid: a person passed the check.</summary>
    Passed,

    /// <summary>The token is missing, expired, already used or forged.</summary>
    Failed,

    /// <summary>No secret is configured, or siteverify could not be reached.</summary>
    Unavailable,
}

/// <summary>Checks a Turnstile token with Cloudflare's siteverify.</summary>
public interface ITurnstileVerifier
{
    ValueTask<TurnstileResult> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default);
}

/// <summary>
/// Posts the token, the secret and the client's address to siteverify. A token is valid once,
/// for five minutes. Errors are logged without the token or the secret.
/// </summary>
public sealed partial class TurnstileVerifier(HttpClient httpClient, IOptions<TurnstileOptions> options, ILogger<TurnstileVerifier> logger) : ITurnstileVerifier
{
    /// <summary>The name of the <see cref="HttpClient"/> registered for siteverify.</summary>
    public const string HttpClientName = "Turnstile";

    public async ValueTask<TurnstileResult> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            LogNoSecret(logger);
            return TurnstileResult.Unavailable;
        }

        var form = new Dictionary<string, string>
        {
            ["secret"] = settings.SecretKey,
            ["response"] = token,
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            form["remoteip"] = remoteIp;
        }

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(settings.VerifyUrl, content, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogSiteverifyStatus(logger, (int)response.StatusCode);
                return TurnstileResult.Unavailable;
            }

            var answer = await response.Content.ReadFromJsonAsync<SiteverifyResponse>(cancellationToken).ConfigureAwait(false);
            if (answer is { Success: true })
            {
                return TurnstileResult.Passed;
            }

            var errorCodes = answer?.ErrorCodes ?? [];
            LogTokenRefused(logger, errorCodes);
            return TurnstileResult.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !cancellationToken.IsCancellationRequested)
        {
            LogSiteverifyUnreachable(logger, ex);
            return TurnstileResult.Unavailable;
        }
    }

    private sealed record SiteverifyResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("error-codes")] string[]? ErrorCodes);

    [LoggerMessage(Level = LogLevel.Error, Message = "Turnstile has no secret key (TurnstileOptions:SecretKey); every request to a form that requires it is refused")]
    private static partial void LogNoSecret(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile siteverify answered {StatusCode}")]
    private static partial void LogSiteverifyStatus(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Turnstile refused a token: {ErrorCodes}")]
    private static partial void LogTokenRefused(ILogger logger, string[] errorCodes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Turnstile siteverify could not be reached")]
    private static partial void LogSiteverifyUnreachable(ILogger logger, Exception exception);
}
