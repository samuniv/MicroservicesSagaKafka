using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using PaymentService.Infrastructure.RateLimiting;

namespace PaymentService.Infrastructure.Swagger;

public class RateLimitingOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var rateLimitingAttribute = context.MethodInfo
            .GetCustomAttributes(true)
            .OfType<RateLimitingAttribute>()
            .FirstOrDefault();

        if (rateLimitingAttribute != null)
        {
            if (operation.Parameters == null)
                operation.Parameters = new List<OpenApiParameter>();

            operation.Description += "\n\n**Rate Limiting:**\n";
            operation.Description += GetRateLimitDescription(rateLimitingAttribute);

            operation.Responses.Add("429", new OpenApiResponse
            {
                Description = "Too Many Requests - Rate limit exceeded"
            });

            operation.Responses.Add("503", new OpenApiResponse
            {
                Description = "Service Unavailable - Concurrent request limit exceeded"
            });
        }
    }

    private string GetRateLimitDescription(RateLimitingAttribute attribute)
    {
        var settings = new RateLimitingSettings();
        var limits = settings.DlqLimits;

        return attribute.EndpointName switch
        {
            "Statistics" => FormatLimits(limits.Statistics),
            "RetryMessage" => FormatLimits(limits.RetryMessage),
            "RetryAll" => FormatLimits(limits.RetryAll),
            _ => "Standard rate limits apply"
        };
    }

    private string FormatLimits(EndpointLimit limit)
    {
        return $"- Maximum {limit.MaxRequestsPerWindow} requests per {limit.WindowInMinutes} minute(s)\n" +
               $"- Maximum {limit.MaxConcurrentRequests} concurrent requests";
    }
} 