using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceDefaults.Auth.Observability;

namespace Hackmum.Bethuya.Tests.Auth.Observability;

/// <summary>
/// In-memory <see cref="IAuthorizationAuditor"/> that captures every recorded
/// <see cref="AuthorizationAuditEvent"/> so functional tests can assert exactly-once emission and the
/// recorded decision/policy/subject-hash without touching logs, metrics, or traces.
/// </summary>
internal sealed class RecordingAuditor : IAuthorizationAuditor
{
    private readonly ConcurrentQueue<AuthorizationAuditEvent> _events = new();

    public IReadOnlyList<AuthorizationAuditEvent> Events => [.. _events];

    public void Record(AuthorizationAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _events.Enqueue(auditEvent);
    }
}

/// <summary>A single captured log write: its rendered message plus every structured state value.</summary>
internal sealed record RecordingLogEntry(LogLevel Level, string Message, IReadOnlyList<string> StateValues)
{
    /// <summary>All captured text (message + structured values) for substring/PII assertions.</summary>
    public IEnumerable<string> AllText => [Message, .. StateValues];
}

/// <summary>Thread-safe sink shared by recording loggers and their provider.</summary>
internal sealed class RecordingLogSink
{
    private readonly ConcurrentQueue<RecordingLogEntry> _entries = new();

    public IReadOnlyList<RecordingLogEntry> Entries => [.. _entries];

    public void Add(RecordingLogEntry entry) => _entries.Enqueue(entry);
}

/// <summary>Minimal <see cref="ILogger"/> that records each write into a <see cref="RecordingLogSink"/>.</summary>
internal sealed class RecordingLogger(RecordingLogSink sink) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var values = new List<string>();
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (var pair in pairs)
            {
                values.Add(pair.Value?.ToString() ?? string.Empty);
            }
        }

        sink.Add(new RecordingLogEntry(logLevel, formatter(state, exception), values));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

/// <summary>Strongly-typed <see cref="ILogger{T}"/> over a <see cref="RecordingLogSink"/> for unit tests.</summary>
internal sealed class RecordingLogger<T>(RecordingLogSink sink) : ILogger<T>
{
    private readonly RecordingLogger _inner = new(sink);

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => _inner.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary><see cref="ILoggerProvider"/> that funnels every category into one shared sink.</summary>
internal sealed class RecordingLoggerProvider(RecordingLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new RecordingLogger(sink);

    public void Dispose() { }
}

/// <summary>Helpers for observing <see cref="System.Diagnostics.Metrics"/> instruments in tests.</summary>
internal static class MetricsTestHarness
{
    /// <summary>One captured counter measurement: instrument name, value, and its tags.</summary>
    public sealed record Measurement(string Instrument, long Value, IReadOnlyDictionary<string, object?> Tags);

    /// <summary>Creates a real <see cref="AuthorizationMetrics"/> backed by a fresh <see cref="IMeterFactory"/>.</summary>
    public static (AuthorizationMetrics Metrics, ServiceProvider Provider) CreateMetrics()
    {
        var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var metrics = new AuthorizationMetrics(provider.GetRequiredService<IMeterFactory>());
        return (metrics, provider);
    }

    /// <summary>
    /// Starts a <see cref="MeterListener"/> capturing long counter measurements from the supplied
    /// <paramref name="metrics"/> instance only (reference-scoped so concurrent tests sharing the meter
    /// name never cross-contaminate) into <paramref name="measurements"/>. Dispose to stop.
    /// </summary>
    public static MeterListener ListenToAuthorizationMeter(
        ConcurrentQueue<Measurement> measurements,
        AuthorizationMetrics metrics)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (ReferenceEquals(instrument.Meter, metrics.Meter))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var tagMap = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                tagMap[tag.Key] = tag.Value;
            }

            measurements.Enqueue(new Measurement(instrument.Name, measurement, tagMap));
        });

        listener.Start();
        return listener;
    }
}

/// <summary>Captures stopped activities from the Bethuya authorization <see cref="ActivitySource"/>.</summary>
internal static class ActivityTestHarness
{
    public static (ActivityListener Listener, ConcurrentQueue<Activity> Activities) Listen()
    {
        var activities = new ConcurrentQueue<Activity>();
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AuthorizationActivity.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Enqueue
        };

        ActivitySource.AddActivityListener(listener);
        return (listener, activities);
    }
}
