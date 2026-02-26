using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OtelDemo.Controllers;

[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    private readonly ILogger<DemoController>  _logger;
    private readonly OtelDemoMetrics          _metrics;

    public DemoController(ILogger<DemoController> logger, OtelDemoMetrics metrics)
    {
        _logger  = logger;
        _metrics = metrics;
    }

    /// <summary>
    /// 普通请求 - 产生一个 Span + 日志 + Metric
    /// </summary>
    [HttpGet("hello")]
    public IActionResult Hello([FromQuery] string name = "World")
    {
        var sw = Stopwatch.StartNew();

        using var span = OtelDemoActivity.Source.StartActivity("demo.hello");
        span?.SetTag("demo.name", name);

        _logger.LogInformation("Hello called with name={Name}", name);
        _metrics.IncrementRequest("/api/demo/hello");

        sw.Stop();
        _metrics.RecordDuration(sw.Elapsed.TotalMilliseconds, "/api/demo/hello");

        return Ok(new { message = $"Hello, {name}!", traceId = Activity.Current?.TraceId.ToString() });
    }

    /// <summary>
    /// 模拟慢请求，Span 带事件和嵌套子 Span
    /// </summary>
    [HttpGet("slow")]
    public async Task<IActionResult> Slow([FromQuery] int ms = 300)
    {
        ms = Math.Clamp(ms, 10, 5000);

        using var span = OtelDemoActivity.Source.StartActivity("demo.slow");
        span?.SetTag("demo.sleep_ms", ms);
        span?.AddEvent(new ActivityEvent("sleep.start"));

        _logger.LogInformation("Slow endpoint sleeping for {Ms}ms", ms);

        using (var child = OtelDemoActivity.Source.StartActivity("demo.slow.inner"))
        {
            child?.SetTag("demo.step", "inner-work");
            await Task.Delay(ms);
        }

        span?.AddEvent(new ActivityEvent("sleep.end"));
        _metrics.IncrementRequest("/api/demo/slow");
        _metrics.RecordDuration(ms, "/api/demo/slow");

        return Ok(new
        {
            slept    = ms,
            traceId  = Activity.Current?.TraceId.ToString(),
            spanId   = Activity.Current?.SpanId.ToString()
        });
    }

    /// <summary>
    /// 模拟错误，Span 状态设置为 Error
    /// </summary>
    [HttpGet("error")]
    public IActionResult Error()
    {
        using var span = OtelDemoActivity.Source.StartActivity("demo.error");

        try
        {
            throw new InvalidOperationException("This is a demo error for tracing!");
        }
        catch (Exception ex)
        {
            span?.SetStatus(ActivityStatusCode.Error, ex.Message);
            span?.AddTag("exception.type", ex.GetType().FullName);
            span?.AddTag("exception.message", ex.Message);
            span?.AddTag("exception.stacktrace", ex.StackTrace);
            _logger.LogError(ex, "Demo error endpoint was hit");
            return StatusCode(500, new
            {
                error   = ex.Message,
                traceId = Activity.Current?.TraceId.ToString()
            });
        }
    }

    /// <summary>
    /// 环境变量总览，用于验证 OTel 配置是否注入正确
    /// </summary>
    [HttpGet("config")]
    public IActionResult Config()
    {
        var otelVars = new Dictionary<string, string?>();
        foreach (var key in new[]
        {
            "OTEL_SERVICE_NAME",
            "OTEL_EXPORTER_OTLP_ENDPOINT",
            "OTEL_EXPORTER_OTLP_HEADERS",
            "OTEL_EXPORTER_OTLP_PROTOCOL",
            "OTEL_RESOURCE_ATTRIBUTES",
            "OTEL_LOG_LEVEL",
        })
        {
            otelVars[key] = Environment.GetEnvironmentVariable(key) ?? "(not set)";
        }

        return Ok(new { otelEnvironment = otelVars });
    }
}
