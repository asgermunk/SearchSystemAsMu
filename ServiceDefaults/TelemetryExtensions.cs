using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

/* OpenTelemetry for the search services (API and LoadBalancer), without a collector:
 *
 *  - metrics are served on /metrics, where Prometheus scrapes them (the pods carry
 *    the prometheus.io/scrape annotation)
 *  - traces go as OTLP to OTEL_EXPORTER_OTLP_TRACES_ENDPOINT (Tempo)
 *  - logs go as OTLP to OTEL_EXPORTER_OTLP_LOGS_ENDPOINT (Loki's /otlp)
 *
 * Without those two variables (a plain "dotnet run") traces and logs are not
 * exported, and the services log to the console as before.
 */
public static class TelemetryExtensions
{
    private const string MetricsPath = "/metrics";

    /// <param name="serviceName">service.name on everything the service sends.</param>
    /// <param name="telemetryNames">The service's own ActivitySource and Meter names.</param>
    public static IHostApplicationBuilder AddSearchTelemetry(
        this IHostApplicationBuilder builder, string serviceName, params string[] telemetryNames)
    {
        // AddOtlpExporter does not read the signal specific OTEL_EXPORTER_OTLP_*_ENDPOINT
        // variables itself (only UseOtlpExporter does, and that would send the
        // metrics over OTLP too), so they are read here and passed in.
        var tracesEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_TRACES_ENDPOINT"];
        var logsEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_LOGS_ENDPOINT"];

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (!string.IsNullOrWhiteSpace(logsEndpoint))
                logging.AddOtlpExporter(otlp => UseHttp(otlp, logsEndpoint));
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    // every Prometheus scrape would otherwise be a trace of its own
                    .AddAspNetCoreInstrumentation(options =>
                        options.Filter = context => context.Request.Path != MetricsPath)
                    .AddHttpClientInstrumentation()
                    .AddSource(telemetryNames);
                if (!string.IsNullOrWhiteSpace(tracesEndpoint))
                    tracing.AddOtlpExporter(otlp => UseHttp(otlp, tracesEndpoint));
            })
            .WithMetrics(metrics => metrics
                // http.server.request.duration - the response time histogram
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // .NET's own meter: CPU time, GC, thread pool queue length, ...
                .AddMeter("System.Runtime")
                .AddMeter(telemetryNames)
                .AddPrometheusExporter());

        return builder;
    }

    // OTLP over HTTP to the full URL given - Loki only accepts OTLP over HTTP
    private static void UseHttp(OtlpExporterOptions otlp, string endpoint)
    {
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        otlp.Endpoint = new Uri(endpoint);
    }

    /// <summary>Serves the metrics on /metrics for Prometheus to scrape.</summary>
    public static WebApplication MapSearchTelemetry(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint(MetricsPath);
        return app;
    }
}
