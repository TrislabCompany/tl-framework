namespace Trislab.Framework.Shared.Health;

/// <summary>Tags of the framework's health checks, read by the health endpoints.</summary>
public static class HealthCheckTags
{
    /// <summary>
    /// A check that only <c>GET /api/health/details</c> runs, never a readiness probe: a dependency whose
    /// outage degrades a feature (file storage) but must not take the instance out of service. A host that
    /// maps its own probes (<c>/readyz</c>) leaves these out with a predicate.
    /// </summary>
    public const string DetailsOnly = "details-only";
}
