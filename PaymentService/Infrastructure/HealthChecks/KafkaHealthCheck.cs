using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PaymentService.Infrastructure.Configuration;

namespace PaymentService.Infrastructure.HealthChecks;

public class KafkaHealthCheck : IHealthCheck
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaHealthCheck> _logger;

    public KafkaHealthCheck(
        IOptions<KafkaSettings> settings,
        ILogger<KafkaHealthCheck> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers,
                MessageTimeoutMs = 5000 // Short timeout for health check
            };

            using var producer = new ProducerBuilder<string, string>(config).Build();
            var metadata = producer.GetMetadata(TimeSpan.FromSeconds(5));

            var details = new Dictionary<string, object>
            {
                { "BootstrapServers", _settings.BootstrapServers },
                { "Topics", metadata.Topics.Select(t => t.Topic).ToList() },
                { "BrokerCount", metadata.Brokers.Count },
                { "LastChecked", DateTime.UtcNow }
            };

            return HealthCheckResult.Healthy("Kafka connection is healthy", data: details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kafka health check failed");
            
            return HealthCheckResult.Unhealthy(
                "Kafka connection is unhealthy", 
                ex,
                new Dictionary<string, object>
                {
                    { "BootstrapServers", _settings.BootstrapServers },
                    { "LastChecked", DateTime.UtcNow },
                    { "ErrorMessage", ex.Message }
                });
        }
    }
} 