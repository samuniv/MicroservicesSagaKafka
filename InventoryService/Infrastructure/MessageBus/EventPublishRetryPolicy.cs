using Microsoft.Extensions.Options;
using InventoryService.Infrastructure.Configuration;

namespace InventoryService.Infrastructure.MessageBus;

public class EventPublishRetryPolicy
{
    private readonly ILogger<EventPublishRetryPolicy> _logger;
    private readonly int _maxRetries;
    private readonly int _initialDelayMs;
    private readonly double _backoffMultiplier;

    public EventPublishRetryPolicy(
        IOptions<InventorySettings> settings,
        ILogger<EventPublishRetryPolicy> logger)
    {
        _logger = logger;
        _maxRetries = settings.Value.Events.EventRetryCount;
        _initialDelayMs = settings.Value.Events.EventRetryDelayMs;
        _backoffMultiplier = settings.Value.Events.EventRetryBackoffMultiplier;
    }

    public async Task ExecuteAsync(Func<Task> operation, string operationName)
    {
        var retryCount = 0;
        var currentDelay = _initialDelayMs;

        while (true)
        {
            try
            {
                await operation();
                if (retryCount > 0)
                {
                    _logger.LogInformation(
                        "Successfully executed {OperationName} after {RetryCount} retries",
                        operationName,
                        retryCount);
                }
                return;
            }
            catch (Exception ex) when (retryCount < _maxRetries)
            {
                retryCount++;
                _logger.LogWarning(
                    ex,
                    "Error during {OperationName}. Retry {RetryCount} of {MaxRetries} after {Delay}ms",
                    operationName,
                    retryCount,
                    _maxRetries,
                    currentDelay);

                await Task.Delay(currentDelay);
                currentDelay = (int)(currentDelay * _backoffMultiplier);
            }
        }
    }
} 