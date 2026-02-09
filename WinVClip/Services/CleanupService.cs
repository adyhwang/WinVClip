using System;
using System.IO;
using System.Linq;

namespace WinVClip.Services
{
    public class CleanupService : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private System.Threading.Timer? _cleanupTimer;
        private bool _disposed;

        public CleanupService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void Start(int retentionDays)
        {
            Stop();
            
            if (retentionDays <= 0)
                return;

            var interval = TimeSpan.FromHours(24).TotalMilliseconds;
            _cleanupTimer = new System.Threading.Timer(CleanupCallback, retentionDays, (long)interval, (long)interval);
        }

        public void Stop()
        {
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
        }

        private void CleanupCallback(object? state)
        {
            var retentionDays = (int)state!;
            PerformCleanup(retentionDays);
        }

        public void PerformCleanup(int retentionDays)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                _databaseService.DeleteOldItems(cutoffDate);
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
