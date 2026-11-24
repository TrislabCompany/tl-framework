using Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace Integration.Tests.Tests.Realtime;

[Collection(TrislabCollectionDefinition.Name)]
public sealed class HubAuthenticationTests
{
    private const string HubPath = "/api/v1/realtime/hub";

    private readonly TrislabWebApplicationFactory _factory;

    public HubAuthenticationTests(TrislabWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Hub_Should_Reject_Unauthenticated_Connections()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(
                "http://localhost" + HubPath,
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    // TestServer has no WebSocket transport, so force long-polling.
                    options.WebSocketFactory = (_, _) => throw new NotSupportedException();
                    options.SkipNegotiation = false;
                    options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                })
            .Build();
        await using var _ = connection;

        await Should.ThrowAsync<Exception>(async () => await connection.StartAsync());
    }
}
