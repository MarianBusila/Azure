using App.Metrics;
using App.Metrics.Reporting;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppMetricsAppInsights
{
    class Program
    {
        private static ILogger Logger;
        private static IMetricsRoot metrics;
        private static IRunMetricsReports reporter;
        private static Stopwatch TotalTime;
        private static Task[] backgroundTasks;
        private static int RecordCount;
        private static int FlushCount;
        private static readonly ThreadLocal<Random> Rnd = new ThreadLocal<Random>(() => new Random(Environment.TickCount));


        static void Main(string[] args)
        {
            var recordEvery = TimeSpan.FromSeconds(2);
            var reportEvery = TimeSpan.FromSeconds(60);

            Init();

            TotalTime = Stopwatch.StartNew();
            using (var cts = new CancellationTokenSource())
            {
                backgroundTasks = new[]
                {
                    Task.Run(() => Record(recordEvery, cts.Token)),
                    Task.Run(() => Report(reportEvery, cts.Token)),
                };

                PrintHelp(recordEvery, reportEvery);

                ConsoleKey consoleKey;
                do
                {
                    consoleKey = Console.ReadKey().Key;
                    switch (consoleKey)
                    {
                        case ConsoleKey.P:
                            PrintMetricsToConsole();
                            break;

                        case ConsoleKey.R:
                            ReportOnDemand();
                            break;
                    }
                }
                while (consoleKey != ConsoleKey.Escape);

                cts.Cancel();
                Task.WaitAll(backgroundTasks, 5000);
            }

            Console.WriteLine();
            Console.WriteLine($"In {TotalTime.Elapsed} the metrics have been recorded {RecordCount} times and TelemetryClient flushed {FlushCount} times.");

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

        static string GetAppInsightsInstrumentationKey(IConfiguration configuration)
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

        private static void PrintHelp(TimeSpan recordEvery, TimeSpan reportEvery)
        {
            const string sep = "--------------------------------------------------------------------------------";
            Console.WriteLine(sep);
            Console.WriteLine("Use following keys to:");
            Console.WriteLine("P     -> print current metrics to console");
            Console.WriteLine("R     -> immediately report current metrics");
            Console.WriteLine("<Esc> -> exit");
            Console.WriteLine(sep);
            Console.WriteLine($"New metrics are being created every {recordEvery.TotalSeconds}s and reported every {reportEvery.TotalSeconds}s.");
            Console.WriteLine(sep);
        }

        private static async Task Record(TimeSpan every, CancellationToken ct)
        {
            var sw = new Stopwatch();
            while (!ct.IsCancellationRequested)
            {
                sw.Restart();

                metrics.Measure.Counter.Increment(SampleMetrics.CounterOne);
                metrics.Measure.Counter.Increment(SampleMetrics.CounterTwo, Rnd.Value.Next(1, 4));
                metrics.Measure.Gauge.SetValue(SampleMetrics.GaugeOne, Rnd.Value.Next(0, 201));
                metrics.Measure.Histogram.Update(SampleMetrics.HistogramOne, Rnd.Value.Next(0, 201));
                var dimension1 = Rnd.Value.Next(0, 2) == 0 ? "failures" : "errors";
                metrics.Measure.Meter.Mark(SampleMetrics.MeterOne, Rnd.Value.Next(0, 6), dimension1);

                try
                {
                    using (metrics.Measure.Timer.Time(SampleMetrics.TimerOne))
                    {
                        await Task.Delay(Rnd.Value.Next(0, 101), ct).ConfigureAwait(false);
                    }

                    Console.Write('.');
                    Interlocked.Increment(ref RecordCount);
                    

                    sw.Stop();
                    var remaining = every - sw.Elapsed;
                    if (remaining < TimeSpan.Zero)
                    {
                        Logger.Warning(
                            "It is impossible to honour repetition every {0}s because the body of the Record method took {1}.",
                            every.TotalSeconds,
                            sw.Elapsed);
                        continue;
                    }

                    await Task.Delay(remaining, ct).ConfigureAwait(false);
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == ct)
                {
                    Logger.Verbose("Record task cancelled.");
                }
            }
        }

        private static async Task Report(TimeSpan every, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(every, ct).ConfigureAwait(false);
                    await Task.WhenAll(reporter.RunAllAsync(ct)).ConfigureAwait(false);
                    Interlocked.Increment(ref FlushCount);
                    Console.Write('*');
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == ct)
                {
                    Logger.Verbose("Report task cancelled.");
                }
            }
        }

        private static void PrintMetricsToConsole()
        {
            var metricsData = metrics.Snapshot.Get();

            foreach (var fmt in metrics.OutputMetricsFormatters)
            {
                using (var ms = new MemoryStream())
                {
                    fmt.WriteAsync(ms, metricsData).GetAwaiter().GetResult();
                    var txt = Encoding.UTF8.GetString(ms.ToArray());
                    Console.WriteLine(txt);
                }
            }
        }

        private static void ReportOnDemand()
            => Task.WaitAll(reporter.RunAllAsync().ToArray());
    }
}
