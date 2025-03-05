using Microsoft.Extensions.Logging;

namespace OrderService.Infrastructure.MessageBus;

public class RetryPolicy
{
    private readonly ILogger _logger;
    private readonly int _maxRetries;
    private readonly int _initialRetryDelayMs;
    private readonly double _retryDelayMultiplier;

    public RetryPolicy(
        ILogger logger,
        int maxRetries = 3,
        int initialRetryDelayMs = 1000,
        double retryDelayMultiplier = 2.0)
    {
        _logger = logger;
        _maxRetries = maxRetries;
        _initialRetryDelayMs = initialRetryDelayMs;
        _retryDelayMultiplier = retryDelayMultiplier;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var retryCount = 0;
        var currentDelay = _initialRetryDelayMs;

        while (true)
        {
            try
            {
                return await operation();
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
                currentDelay = (int)(currentDelay * _retryDelayMultiplier);
            }
        }
    }

    public async Task ExecuteAsync(Func<Task> operation, string operationName)
    {
        var retryCount = 0;
        var currentDelay = _initialRetryDelayMs;

        while (true)
        {
            try
            {
                await operation();
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
                currentDelay = (int)(currentDelay * _retryDelayMultiplier);
            }
        }
    }
} 