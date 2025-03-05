namespace PaymentService.Infrastructure.Configuration;

public class KafkaSettings
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string PaymentsTopic { get; set; } = "payments";
    public string ConsumerGroup { get; set; } = "payment-service";
    public bool EnableAutoCommit { get; set; } = false;
    public int AutoCommitIntervalMs { get; set; } = 5000;
    public string SecurityProtocol { get; set; } = "PLAINTEXT";
    public string SaslMechanism { get; set; } = "PLAIN";

    // DLQ Settings
    public string DeadLetterTopic { get; set; } = "payments.dlq";
    public int MaxRetries { get; set; } = 3;
    public int RetryBackoffMs { get; set; } = 1000;
    public bool EnableDlq { get; set; } = true;

    public RetrySettings RetrySettings { get; set; } = new();
}

public class RetrySettings
{
    public int MaxRetries { get; set; } = 3;
    public int InitialRetryDelayMs { get; set; } = 1000;
    public int MaxRetryDelayMs { get; set; } = 10000;
    public double RetryDelayMultiplier { get; set; } = 2.0;
} 