namespace Trislab.Framework.Web.BotProtection;

/// <summary>
/// Cloudflare Turnstile, the bot check of a public form (docs/bot-protection.md). The platform
/// sets <see cref="SecretKey"/> from the stamp secret <c>turnstile-secret-key</c>
/// (<c>TURNSTILE_SECRET_KEY</c>); locally and in tests it is one of Cloudflare's test secrets.
/// </summary>
public sealed class TurnstileOptions
{
    /// <summary>Cloudflare's test secret that accepts every token, for local runs and tests.</summary>
    public const string TestSecretThatPasses = "1x0000000000000000000000000000000AA";

    /// <summary>Cloudflare's test secret that refuses every token.</summary>
    public const string TestSecretThatFails = "2x0000000000000000000000000000000AA";

    /// <summary>
    /// The widget's secret key. Without it, an endpoint that requires Turnstile refuses every
    /// request (503): a missing secret must never let a form through unchecked.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>The header the front end sends the widget's token in.</summary>
    public string HeaderName { get; set; } = "X-Turnstile-Token";

    /// <summary>Cloudflare's siteverify endpoint.</summary>
    public Uri VerifyUrl { get; set; } = new("https://challenges.cloudflare.com/turnstile/v0/siteverify");

    /// <summary>How long to wait for siteverify before refusing the request.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
