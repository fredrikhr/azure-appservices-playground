using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

using Azure.Core.Diagnostics;

using Microsoft.Extensions.Logging.Configuration;

namespace FredrikHr.AzureAppServicePlayground.MsiUserAuthWebApp;

public sealed class AzureIdentityEventSourceLoggingForwarder : IDisposable
{
    private readonly Dictionary<string, ILogger> loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILoggerFactory loggerFactory;
    private readonly AzureEventSourceListener eventSourceListener;

    public AzureIdentityEventSourceLoggingForwarder(
        ILoggerFactory? loggerFactory = null
        ) : base()
    {
        this.loggerFactory = loggerFactory ?? Microsoft.Extensions.Logging
            .Abstractions.NullLoggerFactory.Instance;
        eventSourceListener = new(OnEventWritten, EventLevel.LogAlways);
    }

    private void OnEventWritten(EventWrittenEventArgs args, string message)
    {
        var category = args.EventSource.Name;
        var logger = GetOrAddLoggerThreadSafe(category);
        var logLevel = GetLoggingLevel(args.Level);
        if (!logger.IsEnabled(logLevel)) return;
        var eventId = new EventId(args.EventId, args.EventName);
        var logState = (args.PayloadNames ?? Enumerable.Empty<string>())
            .Zip(args.Payload!, ToKeyValuePair);
        logger.Log(logLevel, eventId, logState, exception: null, (_args, _except) => message);

        static KeyValuePair<string, object?> ToKeyValuePair(string name, object? payload)
            => new(name, payload);
    }

    private static LogLevel GetLoggingLevel(EventLevel level) => level switch
    {
        EventLevel.Critical => LogLevel.Critical,
        EventLevel.Error => LogLevel.Error,
        EventLevel.Warning => LogLevel.Warning,
        EventLevel.Informational => LogLevel.Information,
        EventLevel.Verbose => LogLevel.Debug,
        EventLevel.LogAlways => LogLevel.Trace,
        _ => LogLevel.None
    };

    private ILogger GetOrAddLoggerThreadSafe(string category)
    {
        ILogger? logger;
        lock (loggers)
            if (loggers.TryGetValue(category, out logger)) return logger;
        logger = loggerFactory.CreateLogger(category);
        lock (loggers) { loggers[category] = logger; }
        return logger;
    }

    public void Dispose()
    {
        ((IDisposable)eventSourceListener).Dispose();
    }
}
