using System.Text.Json;

namespace PaymentService.Infrastructure.MessageBus;

public class DeadLetterMessage
{
    public string OriginalTopic { get; set; }
    public string MessageKey { get; set; }
    public string MessageValue { get; set; }
    public string ErrorReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string ExceptionDetails { get; set; }

    public DeadLetterMessage(
        string originalTopic,
        string messageKey,
        string messageValue,
        string errorReason,
        int retryCount,
        Dictionary<string, string> headers,
        Exception exception = null)
    {
        OriginalTopic = originalTopic;
        MessageKey = messageKey;
        MessageValue = messageValue;
        ErrorReason = errorReason;
        RetryCount = retryCount;
        FailedAt = DateTime.UtcNow;
        Headers = headers ?? new Dictionary<string, string>();
        ExceptionDetails = exception?.ToString();
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static DeadLetterMessage FromJson(string json)
    {
        return JsonSerializer.Deserialize<DeadLetterMessage>(json);
    }
} 