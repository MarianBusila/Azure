using App.Metrics.Counter;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.Meter;
using App.Metrics.Timer;

namespace AppMetricsAppInsights
{
    public static class SampleMetrics
    {
        public static CounterOptions CounterOne => new CounterOptions
        {
            Name = "counter_one",
        };

        public static CounterOptions CounterTwo => new CounterOptions
        {
            Name = "counter_two",
        };
        public static GaugeOptions GaugeOne => new GaugeOptions
        {
            Name = "gauge_one",
            Tags = new App.Metrics.MetricTags(new[] { "prop1", "prop2" }, new[] { "alpha", "beta" }),
        };

        public static HistogramOptions HistogramOne => new HistogramOptions
        {
            Name = "histogram_one",
        };

        public static MeterOptions MeterOne => new MeterOptions
        {
            Name = "meter_one",
        };

        public static TimerOptions TimerOne => new TimerOptions
        {
            Name = "timer_one"
        };

    }
}
