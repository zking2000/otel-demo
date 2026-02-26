using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OtelDemo;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────────────
// OpenTelemetry - 全部从环境变量驱动
//
// 必须的环境变量：
//   OTEL_SERVICE_NAME              服务名称，e.g. "otel-demo"
//   OTEL_EXPORTER_OTLP_ENDPOINT    OTLP gRPC endpoint，e.g. "http://otel-collector:4317"
//
// 可选的环境变量：
//   OTEL_RESOURCE_ATTRIBUTES       附加资源属性，e.g. "deployment.environment=staging"
//   OTEL_EXPORTER_OTLP_HEADERS     额外 HTTP 头，e.g. "X-Scope-OrgID=tenant1"
//                                  （Tempo 多租户 / Loki 多租户必须设置）
//   OTEL_EXPORTER_OTLP_PROTOCOL    协议：grpc（默认）或 http/protobuf
//   OTEL_LOG_LEVEL                 日志级别
// ──────────────────────────────────────────────────────────────────────────────

var serviceName    = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "otel-demo";
var otlpEndpoint   = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
var tenantHeader   = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS") ?? "";

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName)
    .AddTelemetrySdk()
    .AddEnvironmentVariableDetector();

// Traces
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation(opts =>
        {
            opts.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSource(OtelDemoActivity.Source.Name)
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri(otlpEndpoint);
            // OTEL_EXPORTER_OTLP_HEADERS 由 SDK 自动读取，此处额外显示记录
        })
    )
    // Metrics
    .WithMetrics(metrics => metrics
        .SetResourceBuilder(resourceBuilder)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(OtelDemoMetrics.MeterName)
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri(otlpEndpoint);
        })
    );

// Logs → OTLP
builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.SetResourceBuilder(resourceBuilder);
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = new Uri(otlpEndpoint);
    });
    logging.AddConsoleExporter(); // 同时打到 stdout 方便调试
});

builder.Services.AddControllers();
builder.Services.AddSingleton<OtelDemoMetrics>();

var app = builder.Build();

app.MapControllers();

// 健康检查端点
app.MapGet("/health", () => Results.Ok(new
{
    status   = "healthy",
    service  = serviceName,
    otlp     = otlpEndpoint,
    headers  = string.IsNullOrEmpty(tenantHeader) ? "(none)" : tenantHeader
}));

// 快速测试端点
app.MapGet("/", () => Results.Ok(new
{
    message  = "OtelDemo is running",
    time     = DateTimeOffset.UtcNow,
    service  = serviceName
}));

app.Run();
