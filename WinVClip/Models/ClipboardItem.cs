using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinVClip.Models
{
    public class ClipboardItem : INotifyPropertyChanged
    {
        public long Id { get; set; }
        public ClipboardType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string? ImageHash { get; set; }
        public List<string> FilePaths { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public string PreviewText { get; set; } = string.Empty;
        public long? GroupId { get; set; }

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

        public string FormattedTime => CreatedAt.ToString("HH:mm:ss");
        public string FormattedDate => CreatedAt.ToString("MM-dd");

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
                    image.EndInit();
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
