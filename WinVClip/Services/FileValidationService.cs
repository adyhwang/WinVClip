using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WinVClip.Models;

namespace WinVClip.Services
{
    public class FileValidationService
    {
        private readonly object _lock = new object();
        private readonly HashSet<long> _checkingItems = new HashSet<long>();

        private static bool CheckAllFilesExist(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0)
                return false;

            foreach (var path in filePaths)
            {
                if (string.IsNullOrEmpty(path))
                    return false;
                try
                {
                    if (!File.Exists(path) && !Directory.Exists(path))
                        return false;
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }

        private static void SetItemValid(ClipboardItem item, bool isValid)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                item.IsFileValid = isValid;
            });
        }

        public void ValidateItem(ClipboardItem item)
        {
            if (item == null || item.Type != ClipboardType.FileList)
                return;

            if (item.FilePaths == null || item.FilePaths.Count == 0)
            {
                item.IsFileValid = false;
                return;
            }

            lock (_lock)
            {
                if (_checkingItems.Contains(item.Id))
                    return;
                _checkingItems.Add(item.Id);
            }

            Task.Run(() =>
            {
                try
                {
                    bool allExist = CheckAllFilesExist(item.FilePaths);
                    SetItemValid(item, allExist);
                }
                finally
                {
                    lock (_lock)
                    {
                        _checkingItems.Remove(item.Id);
                    }
                }
            });
        }

        public void ValidateItems(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return;

            var fileItems = items.Where(i => i.Type == ClipboardType.FileList && i.IsFileValid == null).ToList();
            if (fileItems.Count == 0) return;

            Task.Run(() =>
            {
                Parallel.ForEach(fileItems, item =>
                {
                    bool allExist = CheckAllFilesExist(item.FilePaths);
                    SetItemValid(item, allExist);
                });
            });
        }

        public void ReValidateAll(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return;

            var fileItems = items.Where(i => i.Type == ClipboardType.FileList).ToList();
            if (fileItems.Count == 0) return;

            foreach (var item in fileItems)
            {
                item.IsFileValid = null;
            }

            ValidateItems(fileItems);
        }
    }
}
