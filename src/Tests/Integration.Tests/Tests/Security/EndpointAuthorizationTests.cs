using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Integration.Tests.Tests.Security;

/// <summary>
/// Every endpoint of the test host needs sign-in unless it is listed here as public, with a reason. The framework
/// is secure by default: the fallback policy (<c>JwtAuthenticationExtensions</c>) requires a signed-in user with the
/// endpoint's permission on every endpoint that carries no authorization metadata, so a forgotten
/// <c>RequireAuthorization()</c> is still protected. Public is an explicit act: <c>AllowAnonymous()</c> on the
/// endpoint, which this test then wants listed.
/// Four checks: the fallback and default policies deny anonymous users; the endpoints marked
/// <see cref="IAllowAnonymous"/> are exactly <see cref="AnonymousEndpoints"/> (a new public endpoint fails until it is
/// listed, an entry that no longer matches fails too, so the list never goes stale); the documentation paths that the
/// path-aware result handler lets through are exactly <see cref="PublicPathPrefixes"/>; and every other endpoint
/// answers a request without a token with 401, whatever its method, before its handler runs.
/// </summary>
[Collection(TrislabCollectionDefinition.Name)]
public sealed class EndpointAuthorizationTests
{
    /// <summary>
    /// The endpoints marked <c>AllowAnonymous()</c>: "METHOD pattern" and why each is public. Keep the reason specific.
    /// </summary>
    private static readonly Dictionary<string, string> AnonymousEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POST api/v{version:apiVersion}/identity/token/issue"] = "Sign-in itself: the caller has no token yet. Rate limited with the auth policy.",
        ["POST api/v{version:apiVersion}/identity/token/refresh"] = "Exchanges a refresh token for a new access token; the access token has expired. Rate limited.",
        ["POST api/v{version:apiVersion}/identity/self-register"] = "A new user registers before signing in. Rate limited.",
        ["POST api/v{version:apiVersion}/identity/forgot-password"] = "A user who cannot sign in asks for a reset link. Rate limited.",
        ["POST api/v{version:apiVersion}/identity/reset-password"] = "A user who cannot sign in sets a new password with the token from the reset link. Rate limited.",
        ["GET api/v{version:apiVersion}/identity/confirm-email"] = "Opened from the link in the confirmation e-mail, before the user has signed in. Rate limited.",
        ["GET /api/v1/sse/stream"] = "The browser's EventSource cannot send an Authorization header; it authenticates with a one-time token issued by the signed-in POST /api/v1/sse/token and passed in the query.",
        ["GET /healthz"] = "Liveness probe of the orchestrator; it holds no token and answers no data.",
        ["GET /readyz"] = "Readiness probe of the orchestrator; it holds no token.",
        ["GET /api/health/live"] = "Liveness probe of the orchestrator; it holds no token and answers no data.",
        ["GET /api/health/ready"] = "Readiness probe of the orchestrator; it holds no token.",
        ["GET /api/health/info"] = "Name, version and environment of the test host, for the smoke tests; no secret.",
        ["GET /"] = "The hello-world root of the test host.",
    };

    /// <summary>
    /// Paths that <c>PathAwareAuthorizationHandler</c> lets through without a token although their endpoints carry no
    /// <c>AllowAnonymous()</c>: the API documentation. A host that does not want it public leaves
    /// <c>OpenApiOptions:Enabled</c> off.
    /// </summary>
    private static readonly Dictionary<string, string> PublicPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/openapi"] = "The OpenAPI documents; the security scan reads them.",
        ["/scalar"] = "The Scalar viewer of those documents and its static assets.",
    };

    private readonly TrislabWebApplicationFactory _factory;

    public EndpointAuthorizationTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FallbackAndDefaultPolicy_Should_DenyAnonymousUsers()
    {
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var fallback = await provider.GetFallbackPolicyAsync();
        var defaultPolicy = await provider.GetDefaultPolicyAsync();

        fallback.ShouldNotBeNull();
        fallback.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().ShouldNotBeEmpty(
            "The fallback policy covers every endpoint without authorization metadata: it must require a signed-in user.");
        defaultPolicy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().ShouldNotBeEmpty(
            "The default policy of RequireAuthorization() must require a signed-in user.");
    }

    [Fact]
    public void AnonymousEndpoints_Should_BeExactlyTheListedOnes()
    {
        var anonymous = Endpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null && !IsPublicPath(e))
            .SelectMany(Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        anonymous.Where(k => !AnonymousEndpoints.ContainsKey(k)).Order().ShouldBeEmpty(
            "These endpoints are public (AllowAnonymous) but not listed in AnonymousEndpoints. Remove AllowAnonymous(), "
            + "or list the endpoint with the reason it is public.");
        AnonymousEndpoints.Keys.Where(k => !anonymous.Contains(k)).Order().ShouldBeEmpty(
            "These entries of AnonymousEndpoints match no public endpoint (removed, renamed or now protected). Remove them.");
        AnonymousEndpoints.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ShouldBeEmpty(
            "Every public endpoint needs a reason.");
    }

    [Fact]
    public void PublicPathPrefixes_Should_MatchEndpointsAndHaveReasons()
    {
        var patterns = Endpoints().Select(e => Path(e)).ToList();

        PublicPathPrefixes.Keys
            .Where(prefix => !patterns.Any(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeEmpty("These public path prefixes match no endpoint. Remove them.");
        PublicPathPrefixes.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ShouldBeEmpty(
            "Every public path needs a reason.");
    }

    [Fact]
    public async Task EveryOtherEndpoint_Should_Return401_When_CalledWithoutAToken()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        var wrong = new List<string>();
        var checkedCount = 0;
        foreach (var endpoint in Endpoints().Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && !IsPublicPath(e)))
        {
            foreach (var method in Methods(endpoint))
            {
                checkedCount++;
                using var request = new HttpRequestMessage(new HttpMethod(method), Url(endpoint.RoutePattern));
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    wrong.Add($"{method} {endpoint.RoutePattern.RawText} -> {(int)response.StatusCode}");
                }
            }
        }

        checkedCount.ShouldBeGreaterThan(100, "The sweep found too few endpoints: the endpoint source changed.");
        wrong.Order().ShouldBeEmpty("These endpoints answered a request without a token with something other than 401.");
    }

    private IEnumerable<RouteEndpoint> Endpoints()
    {
        _ = _factory.Server;
        return _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
    }

    private static string Path(RouteEndpoint endpoint) => "/" + endpoint.RoutePattern.RawText!.TrimStart('/');

    private static bool IsPublicPath(RouteEndpoint endpoint)
        => PublicPathPrefixes.Keys.Any(prefix => Path(endpoint).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Methods(RouteEndpoint endpoint)
    {
        // A hub or a static route has no method restriction: ask with GET.
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 } ? methods : ["GET"];
    }

    private static IEnumerable<string> Keys(RouteEndpoint endpoint)
        => Methods(endpoint).Select(m => $"{m} {endpoint.RoutePattern.RawText}");

    /// <summary>A URL that matches the pattern: a Guid for a guid constraint, 1 for an int or a version, "x" otherwise.</summary>
    private static string Url(RoutePattern pattern)
    {
        var segments = pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart parameter => ParameterValue(parameter),
            _ => string.Empty,
        })));
        return "/" + string.Join('/', segments);
    }

    private static string ParameterValue(RoutePatternParameterPart parameter)
    {
        var constraints = parameter.ParameterPolicies.Select(p => p.Content).ToList();
        if (constraints.Contains("guid", StringComparer.OrdinalIgnoreCase))
        {
            return Guid.Empty.ToString();
        }

        return constraints.Contains("int", StringComparer.OrdinalIgnoreCase)
            || constraints.Contains("apiVersion", StringComparer.OrdinalIgnoreCase)
            ? "1"
            : "x";
    }
}
