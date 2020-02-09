using App.Metrics.Counter;

namespace AppMetricsAppInsights
{
    public static class SampleMetrics
    {
        public static CounterOptions CounterOne => new CounterOptions
        {
            Name = "counter_one",
        };
    }
}
