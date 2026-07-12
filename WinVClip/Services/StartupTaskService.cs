using System;
using System.Diagnostics;
using System.IO;

namespace WinVClip.Services
{
    public class StartupTaskService
    {
        private const string TaskName = "WinVClip_AutoStart";
        private const string LastPathSettingKey = "LastStartupExePath";

        private readonly SettingsService _settingsService;

        public StartupTaskService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public string GetCurrentExePath()
        {
            string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinVClip.exe");
            return Path.GetFullPath(exePath);
        }

        public bool IsStartupEnabled()
        {
            try
            {
                string output = RunSchTasks($"/query /tn \"{TaskName}\"");
                return output.Contains(TaskName);
            }
            catch
            {
                return false;
            }
        }

        public string GetTaskExePath()
        {
            try
            {
                string output = RunSchTasks($"/query /tn \"{TaskName}\" /v /fo list");
                using (var reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("任务要运行的程序:") || line.StartsWith("Task to run:"))
                        {
                            int colonIndex = line.IndexOf(':');
                            if (colonIndex >= 0 && colonIndex + 1 < line.Length)
                            {
                                return line.Substring(colonIndex + 1).Trim().Trim('"');
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public bool EnableStartup()
        {
            try
            {
                string exePath = GetCurrentExePath();
                string args = $"/create /tn \"{TaskName}\" /tr \"\\\"{exePath}\\\"\" /sc onlogon /rl highest /f";
                RunSchTasks(args);
                SaveLastPath(exePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DisableStartup()
        {
            try
            {
                RunSchTasks($"/delete /tn \"{TaskName}\" /f");
                SaveLastPath(null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool HasPathChanged()
        {
            string currentPath = GetCurrentExePath();
            string lastPath = LoadLastPath();

            if (string.IsNullOrEmpty(lastPath))
                return false;

            return !string.Equals(currentPath, lastPath, StringComparison.OrdinalIgnoreCase);
        }

        public void CheckAndUpdatePath()
        {
            if (!IsStartupEnabled())
                return;

            string currentPath = GetCurrentExePath();
            string taskPath = GetTaskExePath();

            if (!string.IsNullOrEmpty(taskPath) &&
                !string.Equals(currentPath, taskPath, StringComparison.OrdinalIgnoreCase))
            {
                DisableStartup();
                EnableStartup();
            }
        }

        private string LoadLastPath()
        {
            try
            {
                return _settingsService.Settings.StartupLastExePath;
            }
            catch
            {
                return null;
            }
        }

        private void SaveLastPath(string path)
        {
            try
            {
                _settingsService.Settings.StartupLastExePath = path;
                _settingsService.SaveSettings();
            }
            catch
            {
            }
        }

        private static string RunSchTasks(string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"schtasks failed: {error}");
                }

                return output;
            }
        }
    }
}
