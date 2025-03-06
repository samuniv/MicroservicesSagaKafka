namespace InventoryService.Infrastructure
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public Topics Topics { get; set; } = new Topics();
        public Security Security { get; set; } = new Security();
        public int MessageTimeoutMs { get; set; } = 5000;
        public bool EnableIdempotence { get; set; } = true;
        public string ClientId { get; set; } = string.Empty;
    }

    public class Topics
    {
        public string InventoryEvents { get; set; } = string.Empty;
        public string OrderEvents { get; set; } = string.Empty;
        public string PaymentEvents { get; set; } = string.Empty;
        public string DeadLetterQueue { get; set; } = string.Empty;
    }

    public class Security
    {
        public string Protocol { get; set; } = "PLAINTEXT";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string SaslMechanism { get; set; } = "PLAIN";
    }
} 