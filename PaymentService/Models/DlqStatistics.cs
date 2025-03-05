namespace PaymentService.Models;

public class DlqStatistics
{
    public int TotalMessages { get; set; }
    public int LastHourMessages { get; set; }
    public Dictionary<string, int> MessagesByTopic { get; set; } = new();
    public Dictionary<string, int> MessagesByErrorType { get; set; } = new();
    public Dictionary<int, int> MessagesByRetryCount { get; set; } = new();
    public DateTime LastMessageTimestamp { get; set; }
    public List<DlqMessageSummary> RecentMessages { get; set; } = new();
}

public class DlqMessageSummary
{
    public string MessageKey { get; set; }
    public string OriginalTopic { get; set; }
    public string ErrorReason { get; set; }
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
}