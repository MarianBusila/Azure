using App.Metrics;
using App.Metrics.Reporting;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.IO;

namespace AppMetricsAppInsights
{
    class Program
    {
        private static ILogger Logger;
        private static IMetricsRoot metrics;
        private static IRunMetricsReports reporter;

        static void Main(string[] args)
        {
            var recordEvery = TimeSpan.FromSeconds(2);
            var reportEvery = TimeSpan.FromSeconds(60);

            Init();
        }

        static void Init()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();

            var logFile = configuration.GetValue("logFile", "ApplicationInsightsSandbox.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Async(x => x.File(logFile))
                .CreateLogger();

            Logger = Log.Logger.ForContext(typeof(Program));

            // read configuration
            var metricsOptions = configuration.GetSection("metrics").Get<MetricsOptions>();
            var instrumentationKey = GetAppInsightsInstrumentationKey(configuration);

            metrics = new MetricsBuilder()
                .Configuration.Configure(metricsOptions)
                .Report.ToApplicationInsights(instrumentationKey)
                .Build();

            reporter = metrics.ReportRunner;

        }

        private static string GetAppInsightsInstrumentationKey(IConfiguration configuration)
        {
            var instrumentationKey = configuration.GetValue<string>("instrumentationKey");
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                var g = Guid.Parse(instrumentationKey);
                if (g != Guid.Empty)
                {
                    return instrumentationKey;
                }
            }

            throw new Exception("You must set non-empty Application Insights instrumentation key in the appsettings.json config as metrics:instrumentationKey.");
        }

    }
}
