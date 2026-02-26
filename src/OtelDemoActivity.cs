using System.Diagnostics;

namespace OtelDemo;
// ReSharper disable once UnusedType.Global

/// <summary>
/// 共享的 ActivitySource，供整个应用产生 Span
/// </summary>
public static class OtelDemoActivity
{
    public static readonly ActivitySource Source = new(
        "OtelDemo",
        "1.0.0"
    );
}
