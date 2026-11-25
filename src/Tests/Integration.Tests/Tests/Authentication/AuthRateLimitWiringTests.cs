using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Tests.Tests.Authentication;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class AuthRateLimitWiringTests
{
    private const string AuthPolicy = "auth";

    // Each entry: route-pattern suffix that uniquely identifies the endpoint.
    private static readonly string[] AuthSensitiveRouteSuffixes =
    {
        "token/issue",
        "token/refresh",
        "forgot-password",
        "reset-password",
        "confirm-email",
        "self-register",
        "token/select",
        "token/switch",
        "invitations/accept",
        // Sending an invitation sends an e-mail: limited like the anonymous mail-sending endpoints.
        "invitations",
    };

    private readonly TrislabWebApplicationFactory _factory;

    public AuthRateLimitWiringTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AuthSensitiveEndpoints_Should_AllHaveAuthRateLimitPolicyAttached()
    {
        _ = _factory.Server;

        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var endpoints = dataSource.Endpoints.OfType<RouteEndpoint>().ToList();

        var missing = new List<string>();

        foreach (var suffix in AuthSensitiveRouteSuffixes)
        {
            var matches = endpoints
                .Where(e => e.RoutePattern.RawText is { } raw &&
                            raw.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                missing.Add($"{suffix} (no matching endpoint registered)");
                continue;
            }

            var anyHasAuthPolicy = matches.Any(e =>
            {
                var policy = e.Metadata.GetMetadata<EnableRateLimitingAttribute>();
                return policy is not null
                    && string.Equals(policy.PolicyName, AuthPolicy, StringComparison.Ordinal);
            });

            if (!anyHasAuthPolicy)
            {
                missing.Add($"{suffix} (no '{AuthPolicy}' rate limit policy on any matching endpoint)");
            }
        }

        missing.ShouldBeEmpty(
            $"Auth-sensitive endpoints missing rate limiting:\n  - {string.Join("\n  - ", missing)}");
    }

    // Not a list to keep up to date: every public Identity endpoint, today's and tomorrow's, is rate limited.
    [Fact]
    public void Every_Anonymous_Identity_Endpoint_Has_The_Auth_Rate_Limit()
    {
        _ = _factory.Server;

        var unlimited = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("/identity/", StringComparison.OrdinalIgnoreCase) == true
                && e.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null
                && e.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName != AuthPolicy)
            .Select(e => $"{string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])} {e.RoutePattern.RawText}")
            .ToList();

        unlimited.ShouldBeEmpty($"Anonymous Identity endpoints without the '{AuthPolicy}' rate limit:\n  - {string.Join("\n  - ", unlimited)}");
    }
}
