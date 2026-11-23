using System.Net;
using System.Text;
using Trislab.Framework.Web.BotProtection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Framework.Tests.Web;

/// <summary>
/// The Turnstile check: the filter refuses a request without a valid token before the handler
/// runs, and the verifier asks siteverify. Cloudflare is replaced by a fake handler, so no test
/// needs the network.
/// </summary>
public sealed class TurnstileTests
{
    [Fact]
    public async Task Filter_Should_Return_400_When_The_Token_Is_Missing()
    {
        var result = await RunFilter(token: null, TurnstileResult.Passed);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Filter_Should_Run_The_Handler_When_The_Token_Passes()
    {
        var result = await RunFilter(token: "token", TurnstileResult.Passed);

        result.ShouldBe("handled");
    }

    [Fact]
    public async Task Filter_Should_Return_403_When_The_Token_Fails()
    {
        var result = await RunFilter(token: "token", TurnstileResult.Failed);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Filter_Should_Return_503_When_The_Check_Is_Unavailable()
    {
        var result = await RunFilter(token: "token", TurnstileResult.Unavailable);

        result.ShouldBeOfType<ProblemHttpResult>().StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task Verifier_Should_Pass_When_Siteverify_Says_Success()
    {
        using var handler = new FakeSiteverify(HttpStatusCode.OK, """{"success":true,"error-codes":[]}""");
        using var client = new HttpClient(handler, disposeHandler: false);
        var verifier = Verifier(client, TurnstileOptions.TestSecretThatPasses);

        var result = await verifier.VerifyAsync("token", "203.0.113.7");

        result.ShouldBe(TurnstileResult.Passed);
        handler.Body.ShouldContain($"secret={TurnstileOptions.TestSecretThatPasses}");
        handler.Body.ShouldContain("response=token");
        handler.Body.ShouldContain("remoteip=203.0.113.7");
    }

    [Fact]
    public async Task Verifier_Should_Fail_When_Siteverify_Refuses_The_Token()
    {
        using var handler = new FakeSiteverify(HttpStatusCode.OK, """{"success":false,"error-codes":["invalid-input-response"]}""");
        using var client = new HttpClient(handler, disposeHandler: false);
        var verifier = Verifier(client, TurnstileOptions.TestSecretThatFails);

        (await verifier.VerifyAsync("token", remoteIp: null)).ShouldBe(TurnstileResult.Failed);
    }

    [Fact]
    public async Task Verifier_Should_Be_Unavailable_Without_A_Secret()
    {
        using var handler = new FakeSiteverify(HttpStatusCode.OK, """{"success":true}""");
        using var client = new HttpClient(handler, disposeHandler: false);
        var verifier = Verifier(client, secret: null);

        (await verifier.VerifyAsync("token", remoteIp: null)).ShouldBe(TurnstileResult.Unavailable);
        handler.Calls.ShouldBe(0, "without a secret, siteverify must not be asked: the request is refused");
    }

    [Fact]
    public async Task Verifier_Should_Be_Unavailable_When_Siteverify_Errors()
    {
        using var handler = new FakeSiteverify(HttpStatusCode.InternalServerError, "oops");
        using var client = new HttpClient(handler, disposeHandler: false);
        var verifier = Verifier(client, TurnstileOptions.TestSecretThatPasses);

        (await verifier.VerifyAsync("token", remoteIp: null)).ShouldBe(TurnstileResult.Unavailable);
    }

    [Fact]
    public void AddTrislabTurnstile_Should_Bind_The_Secret_And_Register_The_Verifier()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["TurnstileOptions:SecretKey"] = TurnstileOptions.TestSecretThatPasses })
            .Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);

        services.AddTrislabTurnstile(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<TurnstileOptions>>().Value.SecretKey.ShouldBe(TurnstileOptions.TestSecretThatPasses);
        provider.GetRequiredService<ITurnstileVerifier>().ShouldBeOfType<TurnstileVerifier>();
    }

    [Fact]
    public void RequireTurnstile_Should_Mark_The_Endpoint()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddTrislabTurnstile(builder.Configuration);
        using var app = builder.Build();

        app.MapPost("/form", () => "ok").RequireTurnstile();

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).Single();
        endpoint.Metadata.GetMetadata<TurnstileProtectedEndpointMetadata>().ShouldNotBeNull();
    }

    private static async Task<object?> RunFilter(string? token, TurnstileResult verdict)
    {
        var services = new ServiceCollection();
        services.AddOptions<TurnstileOptions>();
        services.AddSingleton<ITurnstileVerifier>(new FixedVerifier(verdict));

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (token is not null)
        {
            httpContext.Request.Headers["X-Turnstile-Token"] = token;
        }

        var context = EndpointFilterInvocationContext.Create(httpContext);
        return await new TurnstileEndpointFilter().InvokeAsync(context, _ => ValueTask.FromResult<object?>("handled"));
    }

    private static TurnstileVerifier Verifier(HttpClient client, string? secret) =>
        new(client, Options.Create(new TurnstileOptions { SecretKey = secret }), NullLogger<TurnstileVerifier>.Instance);

    private sealed class FixedVerifier(TurnstileResult verdict) : ITurnstileVerifier
    {
        public ValueTask<TurnstileResult> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(verdict);
    }

    private sealed class FakeSiteverify(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
