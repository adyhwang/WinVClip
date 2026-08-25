using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace WinVClip.Models
{
    public class ClipboardItem : INotifyPropertyChanged
    {
        public long Id { get; set; }
        public ClipboardType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? RichContent { get; set; }
        public string? RichFormat { get; set; }
        public string? CsvContent { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageHash { get; set; }
        public List<string> FilePaths { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public string PreviewText { get; set; } = string.Empty;
        public long? GroupId { get; set; }

        private bool? _isLocalPath;
        public bool IsLocalPath
        {
            get
            {
                if (_isLocalPath == null)
                {
                    _isLocalPath = ComputeIsLocalPath();
                }
                return _isLocalPath.Value;
            }
        }

        private bool ComputeIsLocalPath()
        {
            if (Type != ClipboardType.Text && Type != ClipboardType.RichText)
                return false;

            string content = Content?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(content))
                return false;

            try
            {
                string path = TrimQuotes(content);
                if (Path.IsPathRooted(path) || path.IndexOf(Path.DirectorySeparatorChar) >= 0 || path.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                {
                    return File.Exists(path) || Directory.Exists(path);
                }
            }
            catch { }

            return false;
        }

        private static string TrimQuotes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            text = text.Trim();
            if (text.StartsWith("\"") && text.EndsWith("\""))
                return text.Substring(1, text.Length - 2);
            if (text.StartsWith("'") && text.EndsWith("'"))
                return text.Substring(1, text.Length - 2);
            return text;
        }

        private string? _groupName;
        public string? GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged();
            }
        }

        private bool? _isFileValid;
        public bool? IsFileValid
        {
            get => _isFileValid;
            set
            {
                _isFileValid = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowFileInvalidBadge));
                OnPropertyChanged(nameof(FileItemForeground));
            }
        }

        public bool ShowFileInvalidBadge
        {
            get => Type == ClipboardType.FileList && IsFileValid == false;
        }

        public string FileItemForeground
        {
            get => IsFileValid == false ? "#EF4444" : null;
        }

        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string FormattedTime
        {
            get
            {
                var today = DateTime.Today;
                if (CreatedAt.Date == today)
                {
                    return CreatedAt.ToString("HH:mm");
                }
                else
                {
                    return CreatedAt.ToString("MM/dd");
                }
            }
        }
        public bool HasImage => !string.IsNullOrEmpty(ImagePath);
        public bool ShowImageBadge => HasImage && Type != ClipboardType.Image;

        private System.Windows.Media.ImageSource? _imageThumbnail;
        public System.Windows.Media.ImageSource? ImageThumbnail
        {
            get
            {
                if (_imageThumbnail == null && !string.IsNullOrEmpty(ImagePath))
                {
                    LoadImageThumbnail();
                }
                return _imageThumbnail;
            }
        }

        private void LoadImageThumbnail()
        {
            Action loadAction = () =>
            {
                string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, ImagePath);
                if (System.IO.File.Exists(fullPath))
                {
                    var image = new System.Windows.Media.Imaging.BitmapImage();
                    image.BeginInit();
                    image.UriSource = new System.Uri(fullPath);
                    image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 64;
                    image.EndInit();
                    image.Freeze();
                    _imageThumbnail = image;
                    OnPropertyChanged(nameof(ImageThumbnail));
                }
            };

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(loadAction);
            }
            else
            {
                loadAction();
            }
        }

        public void ClearImageThumbnail()
        {
            _imageThumbnail = null;
        }

        public void ReleaseMemory()
        {
            _imageThumbnail = null;
            Content = null;
            RichContent = null;
            CsvContent = null;
            ImagePath = null;
            ImageHash = null;
            PreviewText = null;
            FilePaths?.Clear();
            _isLocalPath = null;
            _isFileValid = null;
        }

        
        
        public void TrimLargeObjects()
        {
            _imageThumbnail = null;
            RichContent = null;
            CsvContent = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
