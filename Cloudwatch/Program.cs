using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.Runtime.CredentialManagement;

namespace Cloudwatch
{
    #nullable enable
    class Program
    {
        private const string GA_METRICS_NAMESPACE = "SessionCam/BiDirectionalReportProcessor/GoogleAnalytics";

        private const string METRIC_CLIENT_BLOCKED = "Client Blocked";
        private const string METRIC_REPORT_REQUESTS_THROTTLED = "Report Requests-Throttled";
        private const string METRIC_SEGMENT_WRITTEN = "Segment Written";
        private const string METRIC_REPORT_REQUEST = "Report Request";
        private const string METRIC_HTTP_400 = "HTTP 400";
        private const string METRIC_HTTP_401 = "HTTP 401";
        private const string METRIC_HTTP_403 = "HTTP 403";
        private const string METRIC_HTTP_429_FAILED_REQUESTS_TOO_HIGH = "HTTP 429-Failed reqs high";
        private const string METRIC_HTTP_429_OTHER = "HTTP 429-Other";
        private const string METRIC_NAME_429_REQUESTS_PER_DAY = "HTTP 429-Reqs/day";
        private const string METRIC_HTTP_429_REQUESTS_PER_USER_PER_100s = "HTTP 429-Reqs/user/100s";
        private const string METRIC_HTTP_429_REQUESTS_PER_VIEW_PER_DAY = "HTTP 429-Reqs/view/day";
        private const string METRIC_HTTP_429_TOO_MANY_CONCURRENT_CONNECTIONS = "HTTP 429-Concurrent conns";
        private const string METRIC_HTTP_500 = "HTTP 500";
        private const string METRIC_HTTP_502 = "HTTP 502";
        private const string METRIC_HTTP_503_OTHER = "HTTP 503-Other";
        private const string METRIC_HTTP_503_UNAVAILABLE = "HTTP 503-Unavailable";
        private const string METRIC_HTTP_OTHER = "HTTP-Other";

        private const int ONE_DAY_IN_SECONDS = 86400;
        private const string PACIFIC_STANDARD_TIME = "Pacific Standard Time";
        private const string WRITE_METRICS = "Write Metrics";
        private const string READ_METRICS = "Read Metrics";
        private const string WRITE_READ_METRICS = "Read Write Metrics";

        private readonly IAmazonCloudWatch CloudwatchClient = getCloudwatchClient();

        private static IAmazonCloudWatch getCloudwatchClient()
        {
            var sharedFile = new SharedCredentialsFile();
            sharedFile.TryGetProfile("live", out var profile);
            AWSCredentialsFactory.TryGetAWSCredentials(profile, sharedFile, out var credentials);
            return new AmazonCloudWatchClient(credentials);
        }        

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
            MetricDatum[] metrics = BuildMetrics(METRIC_NAME_429_REQUESTS_PER_DAY, StandardUnit.Count, 1);

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
        private static async Task ReadMetrics(Program program, int noOfDays = 5)
        {
            Console.WriteLine(">>>> Reading metrics");

            List<(string, string)> metrics = new List<(string, string)> {
                ("metricRequestClientBlocked", METRIC_CLIENT_BLOCKED),
                ("metricRequestReportRequestsThrottled", METRIC_REPORT_REQUESTS_THROTTLED),
                ("metricRequestSegmentWritten", METRIC_SEGMENT_WRITTEN),
                ("metricRequestReportRequest", METRIC_REPORT_REQUEST),
                ("metricRequest400", METRIC_HTTP_400),
                ("metricRequest401", METRIC_HTTP_401),
                ("metricRequest403", METRIC_HTTP_403),
                ("metricRequest429FailedRequestsTooHigh", METRIC_HTTP_429_FAILED_REQUESTS_TOO_HIGH),
                ("metricRequest429Other", METRIC_HTTP_429_OTHER),
                ("metricRequest429RequestsPerDay", METRIC_NAME_429_REQUESTS_PER_DAY),
                ("metricRequest429RequestsPerUserPer100s", METRIC_HTTP_429_REQUESTS_PER_USER_PER_100s),
                ("metricRequest429RequestsPerViewPerDay", METRIC_HTTP_429_REQUESTS_PER_VIEW_PER_DAY),
                ("metricRequest429TooManyConcurrentConnections", METRIC_HTTP_429_TOO_MANY_CONCURRENT_CONNECTIONS),
                ("metricRequest500", METRIC_HTTP_500),
                ("metricRequest502", METRIC_HTTP_502),
                ("metricRequest503Other", METRIC_HTTP_503_OTHER),
                ("metricRequest503Unavailable", METRIC_HTTP_503_UNAVAILABLE),
                ("metricRequestOther", METRIC_HTTP_OTHER)
            };

        TimeZoneInfo? pst = GetTimeZoneInfo(PACIFIC_STANDARD_TIME);
            if (pst != null)
            {
                Console.WriteLine($"TimeZone: {pst}");
                var currentDateInTimeZone = CurrentDateInTimeZone(pst);
                for (int i = 0; i < noOfDays; i++)
                {
                    DateTime currentDateInPST = currentDateInTimeZone.AddDays(-i);
                    DateTime nextDateInPST = currentDateInPST.AddDays(1).AddSeconds(-1);
                    DateTime startTimeUTC = ConvertDateToUTC(currentDateInPST, pst);
                    DateTime endTimeUTC = ConvertDateToUTC(nextDateInPST, pst);

                    Console.WriteLine($"Getting metrics from [{startTimeUTC}] to [{endTimeUTC}]");

                    foreach ((string metricQueryId, string metricName) in metrics)
                    {
                        GetMetricDataResponse resp = await program.GetMetricsAsync(startTimeUTC, endTimeUTC, metricQueryId, metricName);

                        Console.WriteLine($"Metric [{metricName}] count is [{GetMetricCount(resp)}]");
                    }
                }
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
            return TimeZoneInfo.ConvertTime(time, tzi, TimeZoneInfo.Utc);
        }

        private async Task<GetMetricDataResponse> GetMetricsAsync(DateTime startTimeUTC, DateTime endTimeUTC, string metricQueryId, string metricName)
        {
            var req = new GetMetricDataRequest
            {
                StartTimeUtc = startTimeUTC,
                EndTimeUtc = endTimeUTC,
                MetricDataQueries = new List<MetricDataQuery>
                {
                    new MetricDataQuery
                    {
                        Id = metricQueryId,

                        MetricStat = new MetricStat
                        {
                            Stat = "Sum",
                            Metric = new Metric
                            {
                                MetricName = metricName,
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
