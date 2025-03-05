namespace PaymentService.Infrastructure.RateLimiting;

public class RateLimitingSettings
{
    public int WindowInMinutes { get; set; } = 1;
    public int MaxRequestsPerWindow { get; set; } = 100;
    public int MaxConcurrentRequests { get; set; } = 10;

    public DlqEndpointLimits DlqLimits { get; set; } = new();
}

public class DlqEndpointLimits
{
    public EndpointLimit Statistics { get; set; } = new()
    {
        WindowInMinutes = 1,
        MaxRequestsPerWindow = 60, // One request per second
        MaxConcurrentRequests = 5
    };

    public EndpointLimit RetryMessage { get; set; } = new()
    {
        WindowInMinutes = 1,
        MaxRequestsPerWindow = 30, // One request every 2 seconds
        MaxConcurrentRequests = 3
    };

    public EndpointLimit RetryAll { get; set; } = new()
    {
        WindowInMinutes = 5,
        MaxRequestsPerWindow = 2, // Two requests per 5 minutes
        MaxConcurrentRequests = 1
    };
}

public class EndpointLimit
{
    public int WindowInMinutes { get; set; }
    public int MaxRequestsPerWindow { get; set; }
    public int MaxConcurrentRequests { get; set; }
} 