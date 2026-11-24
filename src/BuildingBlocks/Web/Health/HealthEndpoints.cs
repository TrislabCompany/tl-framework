using Trislab.Framework.Shared.Constants;
using Trislab.Framework.Shared.Health;
using Trislab.Framework.Shared.Identity.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Trislab.Framework.Web.Health;

public static class HealthEndpoints
{
    public sealed record HealthResult(string Status, IEnumerable<HealthEntry> Results);
    public sealed record HealthEntry(string Name, string Status, string? Description, double DurationMs, Dictionary<string, object>? Details = default);
    public static IEndpointRouteBuilder MapTrislabHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Under /api, so the front ends reach it through their /api proxy. The platform's
        // probes, /healthz and /readyz, are mapped in the API host onto the same checks.
        var group = app.MapGroup("/api/health")
                       .WithTags("Health")
                       .AllowAnonymous()
                       .DisableRateLimiting();


        // Liveness: only process up (no external deps)
        group.MapGet("/live",
                async Task<Ok<HealthResult>> (HealthCheckService hc, CancellationToken cancellationToken) =>
                {
                    var report = await hc.CheckHealthAsync(_ => false, cancellationToken);
                    var payload = new HealthResult(
                    Status: report.Status.ToString(),
                    Results: Array.Empty<HealthEntry>());

                    return TypedResults.Ok(payload);
                })
                .WithName("Liveness")
                .WithSummary("Quick process liveness probe.")
                .WithDescription("Reports if the API process is alive. Does not check dependencies.")
                .Produces<HealthResult>(StatusCodes.Status200OK);

        // Readiness: the database and every registered check except the details-only ones. Anonymous,
        // so it answers the overall status only; which check failed is for /details, behind a permission.
        group.MapGet("/ready",
                    async (HealthCheckService hc, CancellationToken cancellationToken) =>
                    {
                        var report = await hc.CheckHealthAsync(c => !c.Tags.Contains(HealthCheckTags.DetailsOnly), cancellationToken);
                        var payload = new HealthResult(report.Status.ToString(), Array.Empty<HealthEntry>());
                        return Results.Json(payload, statusCode: StatusCode(report));
                    })
                    .WithName("Readiness")
                    .WithSummary("Readiness probe with database check.")
                    .WithDescription("Returns 200 if all dependencies are healthy, otherwise 503. The body has the overall status only; GET /api/health/details lists every check.")
                    .Produces<HealthResult>(StatusCodes.Status200OK)
                    .Produces<HealthResult>(StatusCodes.Status503ServiceUnavailable);

        // Details: every check, the details-only ones too, with its status, description and data, for
        // the Admins of a tenant and the operator. Outside the anonymous group, so the fallback policy
        // authenticates the caller before the permission is checked.
        app.MapGet("/api/health/details",
                    async (HealthCheckService hc, CancellationToken cancellationToken) =>
                    {
                        var report = await hc.CheckHealthAsync(cancellationToken: cancellationToken);
                        var results = report.Entries.Select(e =>
                    new HealthEntry(
                        Name: e.Key,
                        Status: e.Value.Status.ToString(),
                        Description: e.Value.Description,
                        DurationMs: e.Value.Duration.TotalMilliseconds,
                        Details: e.Value.Data.ToDictionary(
                            k => k.Key,
                            v => v.Value is null ? "null" : v.Value
                        )));

                        // Always 200: the caller asked for the report, not for a probe answer.
                        return TypedResults.Ok(new HealthResult(report.Status.ToString(), results));
                    })
                    .WithTags("Health")
                    .DisableRateLimiting()
                    .WithName("HealthDetails")
                    .WithSummary("Every health check with its details.")
                    .WithDescription("Runs every check, also the ones the readiness probe skips (file storage), and returns each one's status and data. Needs Health.View.")
                    .RequirePermission(SystemPermissions.Health.View)
                    .Produces<HealthResult>(StatusCodes.Status200OK)
                    .Produces(StatusCodes.Status401Unauthorized)
                    .Produces(StatusCodes.Status403Forbidden);

        return app;
    }

    private static int StatusCode(HealthReport report) => report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;
}
