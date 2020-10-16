using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace Cloudwatch
{
    #nullable enable
    class Program
    {
        private const string GA_METRICS_NAMESPACE = "SessionCam/BiDirectionalReportProcessor/GoogleAnalytics";
        private const string METRIC_NAME_429_REQUESTS_PER_DAY = "429: Requests Per Day";
        private const int ONE_DAY_IN_SECONDS = 86400;
        private const string PACIFIC_STANDARD_TIME = "Pacific Standard Time";
        private const string WRITE_METRICS = "Write Metrics";
        private const string READ_METRICS = "Read Metrics";
        private const string WRITE_READ_METRICS = "Read Write Metrics";

        private readonly IAmazonCloudWatch CloudwatchClient = new AmazonCloudWatchClient();
        private static readonly Random rnd = new Random();

        static void Main(string[] args)
        {
            String command = ParseArgument(args);
            MainAsync(command).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(String command)
        {
            var program = new Program();

            switch (command)
            {
                case WRITE_METRICS:                
                    await WriteMetrics(program);
                    break;

                case READ_METRICS:                
                    await ReadMetrics(program);
                    break;

                case WRITE_READ_METRICS:
                    await WriteMetrics(program);
                    await ReadMetrics(program);
                    break;
            }                      
        }

        private static String ParseArgument(string[] args)
        {
            String result;
            if (args.Length == 1)
            {
                switch (args[0])
                {
                    case "W":
                    case "w":
                        result = WRITE_METRICS;
                        break;
                    case "R":
                    case "r":
                        result = READ_METRICS;
                        break;
                    default:
                        result = WRITE_READ_METRICS;
                        break;
                }
            } else
            {
                result = WRITE_READ_METRICS;
            }
            return result;
        }

        // Write metrics
        private static async Task WriteMetrics(Program program)
        {
            Console.WriteLine(">>>> Writing metrics");
            MetricDatum[] metrics = BuildMetrics(METRIC_NAME_429_REQUESTS_PER_DAY, StandardUnit.Count, 20);

            foreach (var m in metrics)
            {
                Console.WriteLine($"Time [{m.TimestampUtc.ToString("HH:mm:ss")}] UTC, Value [{m.Value}]");
            }

            await program.SendMetricsBatchAsync(GA_METRICS_NAMESPACE, metrics);
        }

        private static MetricDatum[] BuildMetrics(string name, StandardUnit unit, int metricCount)
        {
            return Enumerable
                .Range(0, metricCount)
                .Select(i => BuildMetric(name, unit, rnd.Next(1, 100)))
                .ToArray();
        }

        private static MetricDatum BuildMetric(string name, StandardUnit unit, double value)
        {
            return new MetricDatum
            {
                MetricName = name,
                Unit = unit,
                Value = value,
                TimestampUtc = RoundDown(DateTime.UtcNow, TimeSpan.FromMinutes(1)).AddMinutes(-rnd.Next(0, 30))
            };
        }

        private static DateTime RoundDown(DateTime dt, TimeSpan d)
        {
            var delta = dt.Ticks % d.Ticks;
            return new DateTime(dt.Ticks - delta, dt.Kind);
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

        // Read metrics
        private static async Task ReadMetrics(Program program)
        {
            Console.WriteLine(">>>> Reading metrics");
            TimeZoneInfo? pst = GetTimeZoneInfo(PACIFIC_STANDARD_TIME);
            if (pst != null)
            {
                Console.WriteLine($"TimeZone: {pst}");
                DateTime currentDateInPST = CurrentDateInTimeZone(pst);
                DateTime nextDateInPST = currentDateInPST.AddDays(1).AddSeconds(-1);
                DateTime startTimeUTC = ConvertDateToUTC(currentDateInPST, pst);
                DateTime endTimeUTC = ConvertDateToUTC(nextDateInPST, pst);                

                GetMetricDataResponse resp = await program.GetMetricsAsync(startTimeUTC, endTimeUTC);            

                Console.WriteLine($"Metric count: {GetMetricCount(resp)}");

                if (resp.MetricDataResults.Any())
                    foreach (var r in resp.MetricDataResults)
                        foreach (var rValue in r.Values)
                            Console.WriteLine(rValue);
            }
            else
            {
                Console.WriteLine("Oops, no timezone");
            }
        }

        private static TimeZoneInfo? GetTimeZoneInfo(String timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        private static DateTime CurrentDateInTimeZone(TimeZoneInfo tzi)
        {
            DateTime timeUtc = DateTime.UtcNow;
            Console.WriteLine($"Current time (UTC): {timeUtc}");
            DateTime timeInTargetTimeZone = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, tzi);

            Console.WriteLine(
                "Current time ({0}): {1}",
                tzi.IsDaylightSavingTime(timeInTargetTimeZone) ? tzi.DaylightName : tzi.StandardName,
                timeInTargetTimeZone
            );

            Console.WriteLine(
                "Current date ({0}): {1}",
                tzi.IsDaylightSavingTime(timeInTargetTimeZone) ? tzi.DaylightName : tzi.StandardName,
                timeInTargetTimeZone.Date
            );

            return timeInTargetTimeZone.Date;
        }

        private static DateTime ConvertDateToUTC(DateTime time, TimeZoneInfo tzi)
        {
            DateTime dtUTC = TimeZoneInfo.ConvertTime(time, tzi, TimeZoneInfo.Utc);
            Console.WriteLine("{0} ({1}) = {2} (UTC)",
                        time,
                        tzi.IsDaylightSavingTime(time) ? tzi.DaylightName : tzi.StandardName,
                        dtUTC
                        );
            return dtUTC;
        }

        private async Task<GetMetricDataResponse> GetMetricsAsync(DateTime startTimeUTC, DateTime endTimeUTC)
        {
            Console.WriteLine($"Getting metrics from [{startTimeUTC}] to [{endTimeUTC}]");

            var req = new GetMetricDataRequest
            {
                StartTimeUtc = startTimeUTC,
                EndTimeUtc = endTimeUTC,
                //MaxDatapoints = 10,
                //ScanBy = new ScanBy("TimestampDescending"),
                //NextToken = nextToken,
                MetricDataQueries = new List<MetricDataQuery>
                {
                    new MetricDataQuery
                    {
                        Id = "metricRequest429RequestsPerDay",

                        MetricStat = new MetricStat
                        {
                            Stat = "Sum",
                            Metric = new Metric
                            {
                                MetricName = METRIC_NAME_429_REQUESTS_PER_DAY,
                                Namespace = GA_METRICS_NAMESPACE
                            },
                            Period = ONE_DAY_IN_SECONDS
                        }
                    }
                }
            };
            return await CloudwatchClient.GetMetricDataAsync(req);
        }

        private static double GetMetricCount(GetMetricDataResponse resp)
        {
            double result = 0;
            if (resp.MetricDataResults.Any())
            {
                var values = resp.MetricDataResults[0].Values;
                if (values.Any())
                {
                    result = values[0];
                }
            }
            return result;
        }
    }
}
