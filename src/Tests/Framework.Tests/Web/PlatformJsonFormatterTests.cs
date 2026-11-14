using System.Diagnostics;
using System.Text.Json;
using FSH.Framework.Web.Observability;
using FSH.Framework.Web.Observability.Logging.Serilog;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Framework.Tests.Web;

/// <summary>The log contract of ADR 0062: one JSON line per event, with the contract's fields.</summary>
public sealed class PlatformJsonFormatterTests
{
    [Fact]
    public void Format_Should_Write_One_Json_Line_With_The_Contract_Fields()
    {
        var line = Write(log => log
            .ForContext("SourceContext", "FSH.Modules.Catalog")
            .ForContext("Environment", "dev")
            .ForContext("Stamp", "shr-con-dev")
            .ForContext("Version", "1.2.3")
            .ForContext("Tenant", "acme")
            .Information("Product {ProductId} renamed", 42));

        line.ShouldEndWith("\n");
        line.TrimEnd('\n').ShouldNotContain('\n');
        var json = JsonDocument.Parse(line).RootElement;
        json.GetProperty("timestamp").GetString()!.ShouldMatch(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
        json.GetProperty("level").GetString().ShouldBe("Information");
        json.GetProperty("message").GetString().ShouldBe("Product 42 renamed");
        json.GetProperty("messageTemplate").GetString().ShouldBe("Product {ProductId} renamed");
        json.GetProperty("category").GetString().ShouldBe("FSH.Modules.Catalog");
        json.GetProperty("environment").GetString().ShouldBe("dev");
        json.GetProperty("stamp").GetString().ShouldBe("shr-con-dev");
        json.GetProperty("version").GetString().ShouldBe("1.2.3");
        json.GetProperty("tenant").GetString().ShouldBe("acme");
        json.GetProperty("productId").GetInt32().ShouldBe(42);
        json.TryGetProperty("sourceContext", out _).ShouldBeFalse();
        json.TryGetProperty("exception", out _).ShouldBeFalse();
    }

    [Fact]
    public void Format_Should_Write_The_Trace_And_Span_Of_The_Current_Activity()
    {
        using var activity = StartActivity();

        var json = JsonDocument.Parse(Write(log => log.Warning("Retrying"))).RootElement;

        json.GetProperty("trace_id").GetString().ShouldBe(activity.TraceId.ToHexString());
        json.GetProperty("span_id").GetString().ShouldBe(activity.SpanId.ToHexString());
    }

    [Fact]
    public void Format_Should_Write_The_Exception_With_Its_Type_Message_And_Stack()
    {
        var exception = Throw();

        var json = JsonDocument.Parse(Write(log => log.Error(exception, "Import failed"))).RootElement;

        json.GetProperty("level").GetString().ShouldBe("Error");
        var written = json.GetProperty("exception");
        written.GetProperty("type").GetString().ShouldBe("System.InvalidOperationException");
        written.GetProperty("message").GetString().ShouldBe("The file is empty");
        written.GetProperty("stack").GetString()!.ShouldContain(nameof(Throw));
    }

    [Theory]
    [InlineData(LogEventLevel.Verbose, "Trace")]
    [InlineData(LogEventLevel.Fatal, "Critical")]
    public void Format_Should_Use_The_Contracts_Level_Names(LogEventLevel level, string expected)
    {
        var json = JsonDocument.Parse(Write(log => log.Write(level, "Something"))).RootElement;

        json.GetProperty("level").GetString().ShouldBe(expected);
    }

    [Fact]
    public void Format_Should_Keep_A_Contract_Field_When_A_Property_Has_Its_Name()
    {
        var json = JsonDocument.Parse(Write(log => log.ForContext("Level", "x").Information("Started"))).RootElement;

        json.GetProperty("level").GetString().ShouldBe("Information");
    }

    [Fact]
    public void Id_Should_Be_The_Trace_Id_Of_The_Current_Activity()
    {
        using var activity = StartActivity();

        Correlation.Id(new DefaultHttpContext()).ShouldBe(activity.TraceId.ToHexString());
    }

    // The caller disposes the activity (using var).
#pragma warning disable CA2000
    private static Activity StartActivity() => new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
#pragma warning restore CA2000

    private static string Write(Action<ILogger> log)
    {
        var sink = new CapturingSink();
        using (var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger())
        {
            log(logger);
        }

        using var output = new StringWriter();
        new PlatformJsonFormatter().Format(sink.Events.ShouldHaveSingleItem(), output);
        return output.ToString();
    }

    private static InvalidOperationException Throw()
    {
        try
        {
            throw new InvalidOperationException("The file is empty");
        }
        catch (InvalidOperationException e)
        {
            return e;
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
