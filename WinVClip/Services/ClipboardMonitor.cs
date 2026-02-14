using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip.Services
{
    public class ClipboardMonitor : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;
        private bool _disposed;
        private bool _isMonitoring = true;
        private string _lastClipboardContent = string.Empty;
        private string _lastImageHash = string.Empty;
        private string _lastFileListHash = string.Empty;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private Timer? _timer;
        private readonly object _syncLock = new object();

        public event Action<ClipboardItem>? OnClipboardChanged;
        public event Action? OnDuplicateUpdated;

        public ClipboardMonitor(DatabaseService databaseService, SettingsService settingsService)
        {
            _databaseService = databaseService;
            _settingsService = settingsService;
            _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        }

        public void Start()
        {
            _isMonitoring = _settingsService.Settings.MonitorEnabled;
            _timer?.Dispose();
            
            // 初始化重复检测变量，避免程序重启后重复添加剪贴板内容
            if (_isMonitoring && _settingsService.Settings.MonitorEnabled)
            {
                _dispatcher.Invoke(UpdateLastClipboardState);
            }
            
            _timer = new Timer(CheckClipboard, null, 500, 500);
        }

        private void UpdateLastClipboardState()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    _lastClipboardContent = System.Windows.Clipboard.GetText();
                }
                else if (System.Windows.Clipboard.ContainsImage())
                {
                    var image = System.Windows.Clipboard.GetImage();
                    if (image != null)
                    {
                        var imageData = ImageToBytes(image);
                        _lastImageHash = ComputeHash(imageData);
                    }
                }
                else if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var dataObject = System.Windows.Clipboard.GetDataObject();
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    {
                        var fileList = dataObject.GetData(System.Windows.DataFormats.FileDrop) as string[];
                        if (fileList != null && fileList.Length > 0)
                        {
                            var content = string.Join("|", fileList);
                            _lastFileListHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public void Stop()
        {
            _isMonitoring = false;
            _timer?.Dispose();
            _timer = null;
        }

        private bool _isIgnoringChange = false;

        public void IgnoreNextChange(int milliseconds = 500)
        {
            // 防止并发执行
            if (_isIgnoringChange)
                return;

            try
            {
                _isIgnoringChange = true;
                
                _dispatcher.Invoke(UpdateLastClipboardState);

                // 延迟重置忽略状态，确保足够的时间处理粘贴操作
                if (milliseconds > 0)
                {
                    Task.Delay(milliseconds).ContinueWith(_ =>
                    {
                        _isIgnoringChange = false;
                    });
                }
                else
                {
                    _isIgnoringChange = false;
                }
            }
            catch
            {
                _isIgnoringChange = false;
            }
        }

        private void CheckClipboard(object? state)
        {
            if (!_isMonitoring || !_settingsService.Settings.MonitorEnabled)
                return;

            try
            {
                _dispatcher.Invoke(() =>
                {
                    if (!_isMonitoring || !_settingsService.Settings.MonitorEnabled)
                        return;

                    ProcessClipboard();
                });
            }
            catch
            {
            }
        }

        private void ProcessClipboard()
        {
            try
            {

                bool hasText = false;
                bool hasImage = false;
                bool hasFileDrop = false;

                try
                {
                    hasText = System.Windows.Clipboard.ContainsText();
                    hasImage = System.Windows.Clipboard.ContainsImage();
                    hasFileDrop = System.Windows.Clipboard.ContainsFileDropList();
                }
                catch
                {
                    return;
                }

                if (!hasText && !hasImage && !hasFileDrop)
                    return;

                var item = new ClipboardItem
                {
                    CreatedAt = DateTime.Now
                };

                if (hasText && (_settingsService.Settings.CaptureImages || _settingsService.Settings.CaptureFiles))
                {
                    var text = System.Windows.Clipboard.GetText();
                    if (ShouldProcessText(text))
                    {
                        item.Type = ClipboardType.Text;
                        item.Content = text;
                        item.PreviewText = text;

                        SaveItem(item);
                        return;
                    }
                }

                if (hasImage && _settingsService.Settings.CaptureImages)
                {
                    var image = GetClipboardImage();
                    if (image != null)
                    {
                        ProcessImageClipboard(item, image);
                    }
                    return;
                }

                if (hasFileDrop && _settingsService.Settings.CaptureFiles)
                {
                    var dataObject = System.Windows.Clipboard.GetDataObject();
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    {
                        var fileList = dataObject.GetData(System.Windows.DataFormats.FileDrop) as string[];
                        if (fileList != null && fileList.Length > 0)
                        {
                            var filePaths = fileList.Where(f => File.Exists(f) || Directory.Exists(f)).ToList();
                            var sortedPaths = filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();
                            if (ShouldProcessFileList(filePaths))
                            {
                                item.Type = ClipboardType.FileList;
                                item.FilePaths = filePaths;
                                item.Content = string.Join("\n", sortedPaths);
                                
                                if (filePaths.Count == 1)
                                {
                                    item.PreviewText = Path.GetFileName(filePaths[0]);
                                }
                                else
                                {
                                    var fileNames = filePaths.Take(2).Select(p => Path.GetFileName(p)).ToList();
                                    if (filePaths.Count > 2)
                                    {
                                        fileNames.Add($"……(共{filePaths.Count}个文件/目录)");
                                    }
                                    item.PreviewText = string.Join("\n", fileNames);
                                }
                                
                                SaveItem(item);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private bool ShouldProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (_settingsService.Settings.RemoveDuplicates)
            {
                if (text == _lastClipboardContent)
                    return false;

                if (_databaseService.TextExistsInDatabase(text))
                {
                    _databaseService.UpdateDuplicateItemTimestamp(text, (int)ClipboardType.Text);
                    _lastClipboardContent = text;
                    OnDuplicateUpdated?.Invoke();
                    return false;
                }
            }

            _lastClipboardContent = text;
            return true;
        }

        private bool ShouldProcessImage(byte[] imageData, string imageHash)
        {
            if (imageData == null || imageData.Length == 0)
                return false;

            if (!_settingsService.Settings.RemoveDuplicates)
                return true;

            if (imageHash == _lastImageHash)
                return false;

            if (_databaseService.ImageExistsInDatabase(imageHash))
            {
                _databaseService.UpdateDuplicateImageTimestamp(imageHash);
                _lastImageHash = imageHash;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastImageHash = imageHash;
            return true;
        }

        private bool ShouldProcessFileList(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0)
                return false;

            if (!_settingsService.Settings.RemoveDuplicates)
                return true;

            var sortedPaths = filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();
            var content = string.Join("\n", sortedPaths);
            var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", sortedPaths)));
            
            if (hash == _lastFileListHash)
                return false;

            if (_databaseService.FileListExistsByPaths(filePaths, out var matchingContent))
            {
                if (!string.IsNullOrEmpty(matchingContent))
                {
                    _databaseService.UpdateDuplicateItemTimestamp(matchingContent, (int)ClipboardType.FileList);
                }
                _lastFileListHash = hash;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastFileListHash = hash;
            return true;
        }

        private string ComputeHash(byte[] data)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private void SaveItem(ClipboardItem item)
        {
            // 提前生成缩略图，确保UI显示时已经准备好
            if (item.Type == ClipboardType.Image && !string.IsNullOrEmpty(item.ImagePath))
            {
                try
                {
                    // 访问ImageThumbnail属性触发缩略图生成
                    var thumbnail = item.ImageThumbnail;
                }
                catch
                {
                }
            }

            var id = _databaseService.InsertItem(item);
            item.Id = id;
            OnClipboardChanged?.Invoke(item);

            // 检查并清理超出历史记录数量限制的记录
            if (_settingsService.Settings.EnableAutoCleanup && _settingsService.Settings.MaxHistoryItems > 0)
            {
                _databaseService.CleanupExcessHistoryItems(_settingsService.Settings.MaxHistoryItems);
            }
        }

        private byte[] ImageToBytes(BitmapSource image)
        {
            try
            {
                // 创建可写的BitmapSource副本，确保可以正确编码
                BitmapSource writableBitmap = CreateWritableBitmap(image);
                
                // 尝试多种编码器保存图片
                byte[] result = TryEncodeWithPngEncoder(writableBitmap);
                
                if (result.Length == 0)
                {
                    result = TryEncodeWithJpegEncoder(writableBitmap);
                }
                
                if (result.Length == 0)
                {
                    result = TryEncodeWithBmpEncoder(writableBitmap);
                }
                
                if (result.Length == 0)
                {
                    result = TryEncodeWithGdiPlus(writableBitmap);
                }
                
                return result;
            }
            catch
            {
                return new byte[0];
            }
        }

        private BitmapSource CreateWritableBitmap(BitmapSource source)
        {
            try
            {
                // 确保图片格式为Bgr32或Pbgra32，这是最兼容的格式
                if (source.Format != System.Windows.Media.PixelFormats.Bgr32 &&
                    source.Format != System.Windows.Media.PixelFormats.Pbgra32)
                {
                    // 创建FormatConvertedBitmap来转换格式
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = System.Windows.Media.PixelFormats.Bgr32;
                    converted.EndInit();

                    // 冻结以提高性能
                    converted.Freeze();
                    return converted;
                }

                // 如果已经是兼容格式，直接返回
                return source;
            }
            catch
            {
                return source;
            }
        }

        private byte[] TryEncodeWithPngEncoder(BitmapSource image)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        private byte[] TryEncodeWithJpegEncoder(BitmapSource image)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                encoder.QualityLevel = 95;
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        private byte[] TryEncodeWithBmpEncoder(BitmapSource image)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var encoder = new System.Windows.Media.Imaging.BmpBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        private byte[] TryEncodeWithGdiPlus(BitmapSource image)
        {
            try
            {
                // 将WPF BitmapSource转换为GDI+ Bitmap
                int width = image.PixelWidth;
                int height = image.PixelHeight;
                int stride = width * 4; // 32位格式，每像素4字节
                byte[] pixels = new byte[height * stride];

                image.CopyPixels(pixels, stride, 0);

                // 创建GDI+ Bitmap
                using var gdiBitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Drawing.Imaging.BitmapData bmpData = gdiBitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                gdiBitmap.UnlockBits(bmpData);

                // 使用GDI+保存为PNG
                using var memoryStream = new MemoryStream();
                gdiBitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
                return memoryStream.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        private BitmapSource? GetClipboardImage()
        {
            try
            {
                // 尝试直接获取图像
                var image = System.Windows.Clipboard.GetImage();
                if (image != null)
                    return image;
                    
                // 如果直接获取失败，尝试从DataObject获取
                var dataObject = System.Windows.Clipboard.GetDataObject();
                if (dataObject != null)
                {
                    // 尝试获取不同格式的图像数据
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
                    {
                        var bitmap = dataObject.GetData(System.Windows.DataFormats.Bitmap);
                        if (bitmap is System.Drawing.Bitmap gdiBitmap)
                        {
                            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                gdiBitmap.GetHbitmap(),
                                IntPtr.Zero,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                        }
                    }
                    else if (dataObject.GetDataPresent(System.Windows.DataFormats.Dib))
                    {
                        var dibData = dataObject.GetData(System.Windows.DataFormats.Dib);
                        if (dibData is byte[] dibBytes)
                        {
                            using var stream = new MemoryStream(dibBytes);
                            var bitmapImage = new BitmapImage();
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = stream;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                            return bitmapImage;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 可以添加日志记录
                Console.WriteLine($"Get clipboard image error: {ex.Message}");
            }
            
            return null;
        }

        private void ProcessImageClipboard(ClipboardItem item, BitmapSource image)
        {
            try
            {
                // 创建images文件夹（如果不存在）
                string imagesFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "images");
                System.IO.Directory.CreateDirectory(imagesFolder);
                
                // 获取图片数据
                var imageData = ImageToBytes(image);
                
                if (imageData.Length == 0)
                {
                    return;
                }
                
                // 计算哈希值
                string imageHash = ComputeHash(imageData);
                
                // 检查是否已存在相同哈希值的图片
                if (ShouldProcessImage(imageData, imageHash))
                {
                    // 生成文件名：时间戳+哈希值
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    string extension = ".png"; // 默认使用PNG格式
                    string fileName = $"{timestamp}_{imageHash.Substring(0, 8)}{extension}";
                    string relativePath = System.IO.Path.Combine("images", fileName);
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, relativePath);
                    
                    // 保存图片到文件
                    System.IO.File.WriteAllBytes(fullPath, imageData);
                    
                    // 验证保存的文件是否有效
                    ValidateSavedImage(fullPath);
                    
                    // 设置item属性
                    item.Type = ClipboardType.Image;
                    item.ImagePath = relativePath;
                    item.ImageHash = imageHash;
                    item.PreviewText = $"[图片] {image.PixelWidth}x{image.PixelHeight}";

                    SaveItem(item);
                }
            }
            catch (Exception ex)
            {
                // 可以添加日志记录
                Console.WriteLine($"Process image clipboard error: {ex.Message}");
            }
        }

        private void ValidateSavedImage(string filePath)
        {
            try
            {
                using var testStream = System.IO.File.OpenRead(filePath);
                var testImage = new System.Windows.Media.Imaging.BitmapImage();
                testImage.BeginInit();
                testImage.StreamSource = testStream;
                testImage.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                testImage.EndInit();
            }
            catch (Exception ex)
            {
                // 可以添加日志记录
                Console.WriteLine($"Validate saved image error: {ex.Message}");
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

        private bool IsValidUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // 简单的URL检测：以http://或https://开头
            text = text.Trim();
            return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
        }
    }
}