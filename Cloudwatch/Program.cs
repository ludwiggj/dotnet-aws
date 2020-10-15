using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace Cloudwatch
{
    class Program
    {
        private const string GA_METRICS_NAMESPACE = "SessionCam/BiDirectionalReportProcessor/GoogleAnalytics";
        private const string METRIC_NAME_429_REQUESTS_PER_DAY = "429: Requests Per Day";

        private IAmazonCloudWatch CloudwatchClient = new AmazonCloudWatchClient();
        private Random rnd = new Random();

        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            var program = new Program();
            //var metric = program.BuildMetric(METRIC_NAME_429_REQUESTS_PER_DAY, StandardUnit.Count, 1);
            MetricDatum[] metrics = program.BuildMetrics(METRIC_NAME_429_REQUESTS_PER_DAY, StandardUnit.Count, 20);

            foreach (var m in metrics)
            {
                Console.WriteLine($"Time [{m.TimestampUtc.ToString("HH:mm:ss")}] UTC, Value [{m.Value}]");
            }

            await program.SendMetricsBatchAsync(GA_METRICS_NAMESPACE, metrics);
        }

        private MetricDatum BuildMetric(string name, StandardUnit unit, double value)
        {
            return new MetricDatum
            {
                MetricName = name,
                Unit = unit,
                Value = value,
                TimestampUtc = RoundDown(DateTime.UtcNow, TimeSpan.FromMinutes(1)).AddMinutes(-rnd.Next(0, 30))
            };
        }

        public static DateTime RoundDown(DateTime dt, TimeSpan d)
        {
            var delta = dt.Ticks % d.Ticks;
            return new DateTime(dt.Ticks - delta, dt.Kind);
        }

        private MetricDatum[] BuildMetrics(string name, StandardUnit unit, int metricCount)
        {            
            return Enumerable.Range(0, metricCount)
                .Select(i => BuildMetric(name, unit, rnd.Next(1, 100))).ToArray();
        }

        private async Task SendMetricsBatchAsync(string metricNamespace, params MetricDatum[] metricList)
        {
            try
            {
                PutMetricDataRequest request = new PutMetricDataRequest
                {
                    Namespace = metricNamespace,
                    MetricData = new List<MetricDatum>(metricList)
                };
                await CloudwatchClient.PutMetricDataAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while sending cloudwatch metrics: " + ex);
            }
        }
    }
}
