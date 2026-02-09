using System;
using System.IO;
using System.Linq;

namespace WinVClip.Services
{
    public class BackupService : IDisposable
    {
        private readonly string _databasePath;
        private readonly string _backupDirectory;
        private System.Threading.Timer? _backupTimer;
        private bool _disposed;

        public BackupService(string databasePath)
        {
            _databasePath = databasePath;
            _backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? "", "backups");
            
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }
        }

        public void Start(int frequencyHours, int maxBackups)
        {
            Stop();
            
            var interval = TimeSpan.FromHours(frequencyHours).TotalMilliseconds;
            _backupTimer = new System.Threading.Timer(BackupCallback, maxBackups, (long)interval, (long)interval);
        }

        public void Stop()
        {
            _backupTimer?.Dispose();
            _backupTimer = null;
        }

        private void BackupCallback(object? state)
        {
            var maxBackups = (int)state!;
            PerformBackup(maxBackups);
        }

        public void PerformBackup(int maxBackups)
        {
            try
            {
                if (!File.Exists(_databasePath))
                    return;

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"clipboard_history_{timestamp}.db";
                var backupPath = Path.Combine(_backupDirectory, backupFileName);

                File.Copy(_databasePath, backupPath, true);

                CleanupOldBackups(maxBackups);
            }
            catch
            {
            }
        }

        private void CleanupOldBackups(int maxBackups)
        {
            try
            {
                var backupFiles = Directory.GetFiles(_backupDirectory, "clipboard_history_*.db")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();

                while (backupFiles.Count > maxBackups)
                {
                    var oldestBackup = backupFiles.Last();
                    File.Delete(oldestBackup);
                    backupFiles.RemoveAt(backupFiles.Count - 1);
                }
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
