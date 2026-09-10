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

        private static bool CheckImageExists(ClipboardItem item)
        {
            if (string.IsNullOrEmpty(item.ImagePath))
                return false;
            try
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.ImagePath);
                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckItemExists(ClipboardItem item)
        {
            if (item.Type == ClipboardType.FileList)
                return CheckAllFilesExist(item.FilePaths);

            if (item.Type == ClipboardType.Image)
                return CheckImageExists(item);

            return true;
        }

        public static bool IsItemInvalid(ClipboardItem item)
        {
            return !CheckItemExists(item);
        }

        private static void SetItemValid(ClipboardItem item, bool isValid)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                item.IsFileValid = isValid;
            });
        }

        private static bool IsValidatableType(ClipboardType type)
        {
            return type == ClipboardType.FileList || type == ClipboardType.Image;
        }

        public void ValidateItem(ClipboardItem item)
        {
            if (item == null || !IsValidatableType(item.Type))
                return;

            if (item.Type == ClipboardType.FileList && (item.FilePaths == null || item.FilePaths.Count == 0))
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
                    bool exists = CheckItemExists(item);
                    SetItemValid(item, exists);
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

            var validatableItems = items.Where(i => IsValidatableType(i.Type) && i.IsFileValid == null).ToList();
            if (validatableItems.Count == 0) return;

            Task.Run(() =>
            {
                Parallel.ForEach(validatableItems, item =>
                {
                    bool exists = CheckItemExists(item);
                    SetItemValid(item, exists);
                });
            });
        }

        public void ReValidateAll(IEnumerable<ClipboardItem> items)
        {
            if (items == null) return;

            var validatableItems = items.Where(i => IsValidatableType(i.Type)).ToList();
            if (validatableItems.Count == 0) return;

            foreach (var item in validatableItems)
            {
                item.IsFileValid = null;
            }

            ValidateItems(validatableItems);
        }
    }
}
