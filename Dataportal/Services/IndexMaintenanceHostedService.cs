using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dataportal.Services
{
    public class IndexMaintenanceHostedService : BackgroundService
    {
        private static readonly TimeSpan ScheduledLocalTime = new TimeSpan(2, 0, 0);
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IndexMaintenanceHostedService> _logger;
        private readonly TimeZoneInfo _timeZone;

        public IndexMaintenanceHostedService(IServiceScopeFactory scopeFactory, ILogger<IndexMaintenanceHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _timeZone = ResolveTimeZone();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Index maintenance background service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = GetDelayUntilNextRun();
                if (delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                await RunIndexMaintenanceAsync(stoppingToken);
            }

            _logger.LogInformation("Index maintenance background service stopped.");
        }

        private async Task RunIndexMaintenanceAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var indexService = scope.ServiceProvider.GetRequiredService<IndexMaintenanceService>();

                await indexService.RunPendingIndexesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index maintenance run failed.");
            }
        }

        private TimeSpan GetDelayUntilNextRun()
        {
            var utcNow = DateTimeOffset.UtcNow;
            var localNow = TimeZoneInfo.ConvertTime(utcNow, _timeZone);
            var nextRunLocal = localNow.Date.Add(ScheduledLocalTime);

            if (localNow.TimeOfDay >= ScheduledLocalTime)
            {
                nextRunLocal = nextRunLocal.AddDays(1);
            }

            var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, _timeZone);
            var delay = nextRunUtc - utcNow;

            return delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Toronto");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    return TimeZoneInfo.Local;
                }
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}