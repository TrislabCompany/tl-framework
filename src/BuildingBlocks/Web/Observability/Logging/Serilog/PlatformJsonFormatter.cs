using System.Globalization;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Json;

namespace FSH.Framework.Web.Observability.Logging.Serilog;

/// <summary>
/// Writes each event as one JSON object on one line, with the fields of the
/// platform's log contract (ADR 0062 in tl-cloud-infrastructure, the Logs
/// section of tl-platform's stamps/aca-app/README.md): timestamp, level,
/// message, messageTemplate, category, trace_id, span_id and exception, then
/// every property in camelCase, among them environment, stamp and version
/// (added by <see cref="Extensions.AddHeroLogging"/>) and tenant. One query
/// then finds every line of a request in any product.
/// appsettings.Production.json names it as the console sink's formatter.
/// </summary>
public sealed class PlatformJsonFormatter : ITextFormatter
{
    // The contract's own fields: a property with one of these names is left
    // out, so the line never has a key twice.
    private static readonly HashSet<string> Reserved =
    [
        "timestamp", "level", "message", "messageTemplate", "category", "trace_id", "span_id", "exception",
    ];

    private readonly JsonValueFormatter _values = new(typeTagName: null);

    public void Format(LogEvent logEvent, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(output);

        output.Write("{\"timestamp\":\"");
        output.Write(logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        output.Write("\",\"level\":\"");
        output.Write(Level(logEvent.Level));
        output.Write('"');

        Write(output, "message", logEvent.RenderMessage(CultureInfo.InvariantCulture));
        Write(output, "messageTemplate", logEvent.MessageTemplate.Text);

        if (logEvent.Properties.TryGetValue("SourceContext", out var source) && source is ScalarValue { Value: string category })
        {
            Write(output, "category", category);
        }

        if (logEvent.TraceId is { } traceId)
        {
            Write(output, "trace_id", traceId.ToHexString());
        }

        if (logEvent.SpanId is { } spanId)
        {
            Write(output, "span_id", spanId.ToHexString());
        }

        if (logEvent.Exception is { } exception)
        {
            output.Write(",\"exception\":{");
            output.Write("\"type\":");
            JsonValueFormatter.WriteQuotedJsonString(exception.GetType().FullName ?? exception.GetType().Name, output);
            output.Write(",\"message\":");
            JsonValueFormatter.WriteQuotedJsonString(exception.Message, output);
            // ToString has the stack trace and every inner exception.
            output.Write(",\"stack\":");
            JsonValueFormatter.WriteQuotedJsonString(exception.ToString(), output);
            output.Write('}');
        }

        foreach (var (key, value) in logEvent.Properties)
        {
            if (key == "SourceContext" || key.Length == 0)
            {
                continue;
            }

            var name = char.ToLowerInvariant(key[0]) + key[1..];
            if (Reserved.Contains(name))
            {
                continue;
            }

            output.Write(',');
            JsonValueFormatter.WriteQuotedJsonString(name, output);
            output.Write(':');
            _values.Format(value, output);
        }

        output.Write('}');
        output.Write('\n');
    }

    // The level names of the contract, which are .NET's LogLevel names.
    private static string Level(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => "Trace",
        LogEventLevel.Debug => "Debug",
        LogEventLevel.Information => "Information",
        LogEventLevel.Warning => "Warning",
        LogEventLevel.Error => "Error",
        _ => "Critical",
    };

    private static void Write(TextWriter output, string name, string value)
    {
        output.Write(",\"");
        output.Write(name);
        output.Write("\":");
        JsonValueFormatter.WriteQuotedJsonString(value, output);
    }
}
