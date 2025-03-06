using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace OrderService.Infrastructure.RateLimiting;

[AttributeUsage(AttributeTargets.Method)]
public class RateLimitingAttribute : ActionFilterAttribute
{
    private readonly string _endpointName;
    private readonly IMemoryCache _cache;
    private readonly RateLimitingSettings _settings;
    private static readonly SemaphoreSlim _semaphore = new(1);

    public string EndpointName => _endpointName;

    public RateLimitingAttribute(string endpointName)
    {
        _endpointName = endpointName;
        _cache = new MemoryCache(new MemoryCacheOptions());
        _settings = new RateLimitingSettings();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = GetEndpointLimits(_endpointName);
        if (endpoint == null)
        {
            await next();
            return;
        }

        var user = context.HttpContext.User.Identity?.Name ?? "anonymous";
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cacheKey = $"rate_limit_{_endpointName}_{user}_{ipAddress}";

        try
        {
            await _semaphore.WaitAsync();

            var requestCount = _cache.GetOrCreate<RequestMetrics>(cacheKey, entry =>
            {
                entry.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(endpoint.WindowInMinutes);
                return new RequestMetrics { Count = 0, ConcurrentRequests = 0 };
            });

            if (requestCount.Count >= endpoint.MaxRequestsPerWindow)
            {
                context.Result = new StatusCodeResult((int)HttpStatusCode.TooManyRequests);
                return;
            }

            if (requestCount.ConcurrentRequests >= endpoint.MaxConcurrentRequests)
            {
                context.Result = new StatusCodeResult((int)HttpStatusCode.ServiceUnavailable);
                return;
            }

            requestCount.Count++;
            requestCount.ConcurrentRequests++;
            _cache.Set(cacheKey, requestCount);
        }
        finally
        {
            _semaphore.Release();
        }

        try
        {
            await next();
        }
        finally
        {
            var metrics = _cache.Get<RequestMetrics>(cacheKey);
            if (metrics != null)
            {
                metrics.ConcurrentRequests--;
                _cache.Set(cacheKey, metrics);
            }
        }
    }

    private EndpointLimit? GetEndpointLimits(string endpointName)
    {
        return endpointName switch
        {
            "Create" => _settings.OrderLimits.Create,
            "Get" => _settings.OrderLimits.Get,
            "List" => _settings.OrderLimits.List,
            "Cancel" => _settings.OrderLimits.Cancel,
            _ => null
        };
    }

    private class RequestMetrics
    {
        public int Count { get; set; }
        public int ConcurrentRequests { get; set; }
    }
} 