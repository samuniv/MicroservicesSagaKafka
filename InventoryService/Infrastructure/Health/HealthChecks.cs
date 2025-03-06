using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text;

namespace InventoryService.Infrastructure.Health;

public static class HealthChecks
{
    public static IHealthChecksBuilder AddInventoryHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddHealthChecks()
            .AddDbContextCheck<InventoryDbContext>("database")
            .AddKafka(
                configuration.GetSection("Kafka:BootstrapServers").Value ?? "localhost:9092",
                "kafka",
                failureStatus: HealthStatus.Degraded)
            .AddUrlGroup(
                new Uri(configuration["ExternalServices:PaymentService"] ?? "http://localhost:5002"),
                name: "payment-service",
                failureStatus: HealthStatus.Degraded);
    }

    public static IEndpointRouteBuilder MapInventoryHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new()
        {
            ResponseWriter = WriteHealthCheckResponse
        });

        endpoints.MapHealthChecks("/health/ready", new()
        {
            ResponseWriter = WriteHealthCheckResponse,
            Predicate = (check) => check.Tags.Contains("ready")
        });

        endpoints.MapHealthChecks("/health/live", new()
        {
            ResponseWriter = WriteHealthCheckResponse,
            Predicate = (_) => false
        });

        return endpoints;
    }

    private static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            Status = report.Status.ToString(),
            Duration = report.TotalDuration,
            Info = report.Entries.Select(e => new
            {
                Key = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration,
                Data = e.Value.Data
            })
        };

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    }
} 