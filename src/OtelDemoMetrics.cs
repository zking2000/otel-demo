using System.Diagnostics.Metrics;

namespace OtelDemo;
// ReSharper disable once UnusedType.Global

/// <summary>
/// 自定义 Metrics
/// </summary>
public sealed class OtelDemoMetrics
{
    public const string MeterName = "OtelDemo";

    private readonly Counter<long>     _requestCounter;
    private readonly Histogram<double> _processingTime;

    public OtelDemoMetrics(IMeterFactory factory)
    {
        var meter = factory.Create(MeterName);
        _requestCounter = meter.CreateCounter<long>(
            "demo.requests.total",
            description: "Total number of demo requests"
        );
        _processingTime = meter.CreateHistogram<double>(
            "demo.processing.duration_ms",
            unit: "ms",
            description: "Processing duration in milliseconds"
        );
    }

    public void IncrementRequest(string endpoint) =>
        _requestCounter.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint));

    public void RecordDuration(double ms, string endpoint) =>
        _processingTime.Record(ms, new KeyValuePair<string, object?>("endpoint", endpoint));
}
