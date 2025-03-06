namespace OrderService.Infrastructure.RateLimiting;

public class RateLimitingSettings
{
    public OrderEndpointLimits OrderLimits { get; set; } = new();
}

public class OrderEndpointLimits
{
    public EndpointLimit Create { get; set; } = new()
    {
        MaxRequestsPerWindow = 100,
        WindowInMinutes = 1,
        MaxConcurrentRequests = 10
    };

    public EndpointLimit Get { get; set; } = new()
    {
        MaxRequestsPerWindow = 1000,
        WindowInMinutes = 1,
        MaxConcurrentRequests = 50
    };

    public EndpointLimit List { get; set; } = new()
    {
        MaxRequestsPerWindow = 10,
        WindowInMinutes = 1,
        MaxConcurrentRequests = 5
    };

    public EndpointLimit Cancel { get; set; } = new()
    {
        MaxRequestsPerWindow = 50,
        WindowInMinutes = 1,
        MaxConcurrentRequests = 10
    };
}

public class EndpointLimit
{
    public int MaxRequestsPerWindow { get; set; }
    public int WindowInMinutes { get; set; }
    public int MaxConcurrentRequests { get; set; }
} 