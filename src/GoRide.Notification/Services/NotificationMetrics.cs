using System.Collections.Concurrent;

namespace GoRide.Notification.Services;

/// <summary>
/// Operational metrics counter and latency tracker for notification dispatch operations.
/// Exposes Prometheus-compatible metric counters (notification_dispatches_total, notification_dispatch_latency_ms).
/// </summary>
public static class NotificationMetrics
{
    private static readonly ConcurrentDictionary<string, long> _dispatchCounters = new();
    private static readonly ConcurrentDictionary<string, long> _latencyTrackers = new();

    /// <summary>
    /// Increments dispatch counter metric for a specific channel and status ("push:sent", "email:skipped", etc.).
    /// </summary>

    public static void IncrementDispatchCount(string channel, string status)
    {
        var key = $"{channel}:{status}".ToLowerInvariant();
        _dispatchCounters.AddOrUpdate(key, 1, (_, current) => current + 1);
    }

    /// <summary>
    /// Records dispatch latency in milliseconds for operational metrics reporting.
    /// </summary>
    public static void RecordLatency(string channel, int latencyMs)
    {
        var key = $"{channel}:total_latency_ms".ToLowerInvariant();
        _latencyTrackers.AddOrUpdate(key, latencyMs, (_, current) => current + latencyMs);
    }

    /// <summary>
    /// Gets current dispatch count for a metric key.
    /// </summary>
    public static long GetDispatchCount(string channel, string status)
    {
        var key = $"{channel}:{status}".ToLowerInvariant();
        return _dispatchCounters.TryGetValue(key, out var val) ? val : 0;
    }
}
