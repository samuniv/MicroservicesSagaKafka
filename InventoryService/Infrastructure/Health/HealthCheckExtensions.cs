using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using InventoryService.Infrastructure.HealthChecks;

namespace InventoryService.Infrastructure.Health
{
    public static class HealthCheckExtensions
    {
        public static IServiceCollection AddInventoryHealthChecks(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddHealthChecks()
                .AddCheck<KafkaHealthCheck>(
                    "kafka_health",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "kafka", "messaging" })
                .AddDbContextCheck<Data.InventoryDbContext>(
                    "database_health",
                    failureStatus: HealthStatus.Unhealthy,
                    tags: new[] { "database" });

            return services;
        }

        public static IEndpointRouteBuilder MapInventoryHealthChecks(
            this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = WriteHealthCheckResponse,
                AllowCachingResponses = false,
                ResultStatusCodes =
                {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });

            return endpoints;
        }

        private static Task WriteHealthCheckResponse(
            HttpContext context,
            HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var response = new
            {
                status = report.Status.ToString(),
                duration = report.TotalDuration,
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    duration = entry.Value.Duration,
                    description = entry.Value.Description,
                    error = entry.Value.Exception?.Message,
                    tags = entry.Value.Tags
                })
            };

            return context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
    }
} 