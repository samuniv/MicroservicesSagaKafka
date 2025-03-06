using Prometheus;

namespace InventoryService.Infrastructure.Metrics
{
    public class MetricsRegistry
    {
        // Counters
        public static readonly Counter InventoryOperations = Metrics.CreateCounter(
            "inventory_operations_total",
            "Total number of inventory operations",
            new CounterConfiguration
            {
                LabelNames = new[] { "operation", "status" }
            });

        public static readonly Counter MessageProcessed = Metrics.CreateCounter(
            "messages_processed_total",
            "Total number of messages processed",
            new CounterConfiguration
            {
                LabelNames = new[] { "topic", "status" }
            });

        // Gauges
        public static readonly Gauge StockLevel = Metrics.CreateGauge(
            "inventory_stock_level",
            "Current stock level for products",
            new GaugeConfiguration
            {
                LabelNames = new[] { "product_id", "product_name" }
            });

        public static readonly Gauge ReservedStock = Metrics.CreateGauge(
            "inventory_reserved_stock",
            "Current reserved stock for products",
            new GaugeConfiguration
            {
                LabelNames = new[] { "product_id", "product_name" }
            });

        // Histograms
        public static readonly Histogram OperationDuration = Metrics.CreateHistogram(
            "inventory_operation_duration_seconds",
            "Duration of inventory operations",
            new HistogramConfiguration
            {
                LabelNames = new[] { "operation" },
                Buckets = new[] { .001, .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 }
            });

        public static readonly Histogram MessageProcessingDuration = Metrics.CreateHistogram(
            "message_processing_duration_seconds",
            "Duration of message processing",
            new HistogramConfiguration
            {
                LabelNames = new[] { "topic" },
                Buckets = new[] { .001, .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10 }
            });

        // Summaries
        public static readonly Summary StockUpdateSize = Metrics.CreateSummary(
            "inventory_stock_update_size",
            "Size of stock updates",
            new SummaryConfiguration
            {
                LabelNames = new[] { "operation" },
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.5, 0.05),
                    new QuantileEpsilonPair(0.9, 0.05),
                    new QuantileEpsilonPair(0.95, 0.01),
                    new QuantileEpsilonPair(0.99, 0.01)
                }
            });
    }
} 