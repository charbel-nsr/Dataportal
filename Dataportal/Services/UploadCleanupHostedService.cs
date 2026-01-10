using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Dataportal.Services
{
    public class UploadCleanupHostedService : BackgroundService
    {
        private const string UploadCacheFolderName = "dataportal-upload-cache";
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
        private static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CleanupInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCleanupAsync(stoppingToken);

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static Task RunCleanupAsync(CancellationToken stoppingToken)
        {
            var rootPath = Path.Combine(Path.GetTempPath(), UploadCacheFolderName);
            if (!Directory.Exists(rootPath))
            {
                return Task.CompletedTask;
            }

            var cutoffUtc = DateTime.UtcNow - MaxSessionAge;

            foreach (var directory in Directory.EnumerateDirectories(rootPath).ToList())
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var lastWriteUtc = Directory.GetLastWriteTimeUtc(directory);
                    if (lastWriteUtc < cutoffUtc)
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch
                {
                    // Best-effort cleanup for stale upload sessions.
                }
            }

            return Task.CompletedTask;
        }
    }
}