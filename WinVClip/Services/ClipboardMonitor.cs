using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
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
        private readonly Dictionary<ClipboardType, string> _lastContentSignatures = new Dictionary<ClipboardType, string>();
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        private HwndSource? _hwndSource;
        private IntPtr _hwnd;
        private bool _isListenerRegistered;

        private readonly object _debounceLock = new object();
        private bool _debouncePending;
        private const int DebounceIntervalMs = 80;
        private System.Windows.Threading.DispatcherTimer? _debounceDispatcherTimer;

        private System.Threading.Timer? _postProcessGcTimer;

        private const int WM_CLIPBOARDUPDATE = 0x031D;

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

            EnsureMessageWindow();

            if (_isMonitoring && _settingsService.Settings.MonitorEnabled)
            {
                _dispatcher.Invoke(UpdateLastClipboardState);
            }

            RegisterListener();
        }

        private void EnsureMessageWindow()
        {
            if (_hwndSource != null)
                return;

            var parameters = new HwndSourceParameters("WinVClipClipboardMonitor")
            {
                WindowStyle = 0,
                PositionX = 0,
                PositionY = 0,
                Width = 0,
                Height = 0,
            };

            _hwndSource = new HwndSource(parameters);
            _hwnd = _hwndSource.Handle;
            _hwndSource.AddHook(WndProc);
        }

        private void RegisterListener()
        {
            if (_isListenerRegistered || _hwnd == IntPtr.Zero)
                return;

            _isListenerRegistered = AddClipboardFormatListener(_hwnd);
        }

        private void UnregisterListener()
        {
            if (!_isListenerRegistered || _hwnd == IntPtr.Zero)
                return;

            RemoveClipboardFormatListener(_hwnd);
            _isListenerRegistered = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdateMessage();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardUpdateMessage()
        {
            if (!_isMonitoring || !_settingsService.Settings.MonitorEnabled)
                return;

            if (CheckAndClearPasteOperation())
                return;

            if (_isIgnoringChange)
                return;

            ScheduleDebouncedProcess();
        }

        private void ScheduleDebouncedProcess()
        {
            lock (_debounceLock)
            {
                if (_debouncePending)
                    return;

                _debouncePending = true;
            }

            if (_debounceDispatcherTimer == null)
            {
                _debounceDispatcherTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DebounceIntervalMs)
                };
                _debounceDispatcherTimer.Tick += OnDebounceTimerTick;
            }
            _debounceDispatcherTimer.Stop();
            _debounceDispatcherTimer.Start();
        }

        private void OnDebounceTimerTick(object? sender, EventArgs e)
        {
            _debounceDispatcherTimer.Stop();

            lock (_debounceLock)
            {
                _debouncePending = false;
            }

            if (!_isMonitoring || !_settingsService.Settings.MonitorEnabled)
                return;

            try
            {
                ProcessClipboard();
            }
            catch
            {
            }
        }

        private void UpdateLastClipboardState()
        {
            _lastContentSignatures.Clear();

            try
            {
                var dataObject = System.Windows.Clipboard.GetDataObject();
                bool hasText = System.Windows.Clipboard.ContainsText();
                bool hasImage = System.Windows.Clipboard.ContainsImage();
                bool hasFileDrop = System.Windows.Clipboard.ContainsFileDropList();
                bool hasRichText = dataObject?.GetDataPresent(System.Windows.DataFormats.Html) == true ||
                                   dataObject?.GetDataPresent(System.Windows.DataFormats.Rtf) == true;

                if (hasRichText && hasText)
                {
                    string richContent = null;
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.Html))
                        richContent = dataObject.GetData(System.Windows.DataFormats.Html) as string;
                    else if (dataObject.GetDataPresent(System.Windows.DataFormats.Rtf))
                        richContent = dataObject.GetData(System.Windows.DataFormats.Rtf) as string;

                    var text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(richContent))
                    {
                        _lastContentSignatures[ClipboardType.RichText] = ComputeSignature(ClipboardType.RichText, text + richContent);
                    }
                }
                else if (hasText)
                {
                    var text = System.Windows.Clipboard.GetText();
                    _lastContentSignatures[ClipboardType.Text] = ComputeSignature(ClipboardType.Text, text);
                }

                if (hasImage)
                {
                    var image = System.Windows.Clipboard.GetImage();
                    if (image != null)
                    {
                        _lastContentSignatures[ClipboardType.Image] = ComputeSignature(ClipboardType.Image, $"{image.PixelWidth}x{image.PixelHeight}_{image.Format}");
                    }
                }

                if (hasFileDrop)
                {
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
                    {
                        var fileList = dataObject.GetData(System.Windows.DataFormats.FileDrop) as string[];
                        if (fileList != null && fileList.Length > 0)
                        {
                            var sortedPaths = fileList.OrderBy(p => p.ToLowerInvariant()).ToList();
                            var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", sortedPaths)));
                            _lastContentSignatures[ClipboardType.FileList] = ComputeSignature(ClipboardType.FileList, hash);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string ComputeSignature(ClipboardType type, string content)
        {
            return $"{(int)type}:{content}";
        }

        public void Stop()
        {
            _isMonitoring = false;
            UnregisterListener();
        }

        private bool _isIgnoringChange = false;
        private bool _isPasteOperation = false;
        private string _lastPasteHash = string.Empty;

        public void IgnoreNextChange(int milliseconds = 500)
        {
            if (_isIgnoringChange)
                return;

            _isIgnoringChange = true;
            try
            {
                _dispatcher.Invoke(UpdateLastClipboardState);
            }
            finally
            {
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
        }

        public void MarkPasteOperation()
        {
            _isPasteOperation = true;
        }

        public void SetLastPasteHashText(string text)
        {
            _lastPasteHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
        }

        public void SetLastPasteHashFiles(List<string> filePaths)
        {
            var sortedPaths = filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();
            _lastPasteHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", sortedPaths)));
        }

        private bool IsRecentlyPastedContent(string content)
        {
            if (string.IsNullOrEmpty(_lastPasteHash) || string.IsNullOrEmpty(content))
                return false;
            var contentHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
            return contentHash == _lastPasteHash;
        }

        public bool CheckAndClearPasteOperation()
        {
            if (_isPasteOperation)
            {
                _isPasteOperation = false;
                return true;
            }
            return false;
        }

        private void ProcessClipboard()
        {
            bool hasText = false;
            bool hasImage = false;
            bool hasFileDrop = false;
            bool hasRichText = false;

            try
            {
                hasText = System.Windows.Clipboard.ContainsText();
                hasImage = System.Windows.Clipboard.ContainsImage();
                hasFileDrop = System.Windows.Clipboard.ContainsFileDropList();
                
                var dataObject = System.Windows.Clipboard.GetDataObject();
                hasRichText = dataObject.GetDataPresent(System.Windows.DataFormats.Html) || 
                              dataObject.GetDataPresent(System.Windows.DataFormats.Rtf);
            }
            catch
            {
                return;
            }

            if (!hasText && !hasImage && !hasFileDrop && !hasRichText)
                return;

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
                            var item = new ClipboardItem
                            {
                                Type = ClipboardType.FileList,
                                FilePaths = filePaths,
                                Content = string.Join("\n", sortedPaths),
                                CreatedAt = DateTime.Now
                            };
                            
                            if (filePaths.Count == 1)
                            {
                                item.PreviewText = Path.GetFileName(filePaths[0]);
                            }
                            else
                            {
                                var fileNames = filePaths.Take(2).Select(p => Path.GetFileName(p)).ToList();
                                if (filePaths.Count > 2)
                                {
                                    fileNames.Add(string.Format(Loc.Get("Preview.FilesAndFoldersShort", "……(共{0}个文件/目录)"), filePaths.Count));
                                }
                                item.PreviewText = string.Join("\n", fileNames);
                            }
                            
                            SaveItem(item);
                        }
                        return;
                    }
                }
            }

            if (hasRichText && hasText && _settingsService.Settings.CaptureImages)
            {
                var dataObject = System.Windows.Clipboard.GetDataObject();
                string richContent = null;
                string richFormat = null;
                string csvContent = null;

                if (dataObject.GetDataPresent(System.Windows.DataFormats.Html))
                {
                    richContent = dataObject.GetData(System.Windows.DataFormats.Html) as string;
                    richFormat = "HTML";
                }
                else if (dataObject.GetDataPresent(System.Windows.DataFormats.Rtf))
                {
                    richContent = dataObject.GetData(System.Windows.DataFormats.Rtf) as string;
                    richFormat = "RTF";
                }

                if (dataObject.GetDataPresent(System.Windows.DataFormats.CommaSeparatedValue))
                {
                    csvContent = dataObject.GetData(System.Windows.DataFormats.CommaSeparatedValue) as string;
                }

                var text = System.Windows.Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(richContent))
                {
                    if (ShouldProcessRichText(text, richContent, richFormat))
                    {
                        string imagePath = null;
                        string imageHash = null;

                        if (hasImage)
                        {
                            var image = GetClipboardImage();
                            if (image != null)
                            {
                                var imageData = ImageToBytes(image);
                                if (imageData.Length > 0)
                                {
                                    imageHash = ComputeHash(imageData);
                                    
                                    string imagesFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "images");
                                    System.IO.Directory.CreateDirectory(imagesFolder);
                                    
                                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                                    string fileName = $"{timestamp}_{imageHash.Substring(0, 8)}.png";
                                    imagePath = System.IO.Path.Combine("images", fileName);
                                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, imagePath);
                                    
                                    System.IO.File.WriteAllBytes(fullPath, imageData);
                                    imageData = null;
                                }
                                image = null;
                            }
                        }

                        var item = new ClipboardItem
                        {
                            Type = ClipboardType.RichText,
                            Content = text,
                            RichContent = richContent,
                            RichFormat = richFormat,
                            CsvContent = csvContent,
                            ImagePath = imagePath,
                            ImageHash = imageHash,
                            PreviewText = text.Length > 100 ? text.Substring(0, 100) : text,
                            CreatedAt = DateTime.Now
                        };
                        SaveItem(item);
                        if (hasImage)
                            SchedulePostProcessGc();
                    }
                    return;
                }
            }

            if (hasImage && _settingsService.Settings.CaptureImages)
            {
                var image = GetClipboardImage();
                if (image != null)
                {
                    ProcessImageClipboard(image);
                    image = null;
                }
                return;
            }

            if (hasText && _settingsService.Settings.CaptureImages)
            {
                var text = System.Windows.Clipboard.GetText();
                if (ShouldProcessText(text))
                {
                    var item = new ClipboardItem
                    {
                        Type = ClipboardType.Text,
                        Content = text,
                        PreviewText = text.Length > 100 ? text.Substring(0, 100) : text,
                        CreatedAt = DateTime.Now
                    };
                    SaveItem(item);
                }
            }
        }

        private bool ShouldProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var signature = ComputeSignature(ClipboardType.Text, text);
            
            if (_lastContentSignatures.TryGetValue(ClipboardType.Text, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.TextExistsInDatabase(text))
            {
                // 如果是刚刚粘贴的内容，不更新时间戳（避免粘贴后移动位置）
                if (!IsRecentlyPastedContent(text))
                {
                    _databaseService.UpdateDuplicateItemTimestamp(text, (int)ClipboardType.Text);
                }
                _lastContentSignatures[ClipboardType.Text] = signature;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastContentSignatures[ClipboardType.Text] = signature;
            return true;
        }

        private bool ShouldProcessRichText(string text, string richContent, string richFormat)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var signature = ComputeSignature(ClipboardType.RichText, text + richContent);
            
            if (_lastContentSignatures.TryGetValue(ClipboardType.RichText, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.RichTextExistsInDatabase(text, richContent))
            {
                // 如果是刚刚粘贴的内容，不更新时间戳（避免粘贴后移动位置）
                if (!IsRecentlyPastedContent(text))
                {
                    _databaseService.UpdateDuplicateRichTextTimestamp(text, richContent);
                }
                _lastContentSignatures[ClipboardType.RichText] = signature;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastContentSignatures[ClipboardType.RichText] = signature;
            return true;
        }

        private bool ShouldProcessImage(byte[] imageData, string imageHash)
        {
            if (imageData == null || imageData.Length == 0)
                return false;

            var signature = ComputeSignature(ClipboardType.Image, imageHash);
            
            if (_lastContentSignatures.TryGetValue(ClipboardType.Image, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.ImageExistsInDatabase(imageHash))
            {
                // 如果是刚刚粘贴的内容，不更新时间戳（避免粘贴后移动位置）
                if (!IsRecentlyPastedContent(imageHash))
                {
                    _databaseService.UpdateDuplicateImageTimestamp(imageHash);
                }
                _lastContentSignatures[ClipboardType.Image] = signature;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastContentSignatures[ClipboardType.Image] = signature;
            return true;
        }

        private bool ShouldProcessFileList(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0)
                return false;

            var sortedPaths = filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();
            var content = string.Join("\n", sortedPaths);
            var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", sortedPaths)));
            var signature = ComputeSignature(ClipboardType.FileList, hash);
            
            if (_lastContentSignatures.TryGetValue(ClipboardType.FileList, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.FileListExistsByPaths(filePaths, out var matchingContent))
            {
                if (!string.IsNullOrEmpty(matchingContent))
                {
                    // 如果是刚刚粘贴的内容，不更新时间戳（避免粘贴后移动位置）
                    if (!IsRecentlyPastedContent(hash))
                    {
                        _databaseService.UpdateDuplicateItemTimestamp(matchingContent, (int)ClipboardType.FileList);
                    }
                }
                _lastContentSignatures[ClipboardType.FileList] = signature;
                OnDuplicateUpdated?.Invoke();
                return false;
            }

            _lastContentSignatures[ClipboardType.FileList] = signature;
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
            var id = _databaseService.InsertItem(item);
            item.Id = id;
            OnClipboardChanged?.Invoke(item);

            if (_settingsService.Settings.EnableAutoCleanup && _settingsService.Settings.MaxHistoryItems > 0)
            {
                _databaseService.CleanupExcessHistoryItems(_settingsService.Settings.MaxHistoryItems);
            }
        }

        private void SchedulePostProcessGc()
        {
            _postProcessGcTimer?.Dispose();
            _postProcessGcTimer = new System.Threading.Timer(_ =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, true);
                try
                {
                    using var proc = System.Diagnostics.Process.GetCurrentProcess();
                    SetProcessWorkingSetSize(proc.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
                catch { }
                _postProcessGcTimer?.Dispose();
                _postProcessGcTimer = null;
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(-1));
        }

        private byte[] ImageToBytes(BitmapSource image)
        {
            try
            {
                BitmapSource processImage = image;
                
                const int maxDimension = 1920;
                if (image.PixelWidth > maxDimension || image.PixelHeight > maxDimension)
                {
                    double scale = Math.Min((double)maxDimension / image.PixelWidth, (double)maxDimension / image.PixelHeight);
                    int newWidth = (int)(image.PixelWidth * scale);
                    int newHeight = (int)(image.PixelHeight * scale);
                    
                    var scaledBitmap = new System.Windows.Media.Imaging.TransformedBitmap(image, new System.Windows.Media.ScaleTransform(scale, scale));
                    if (scaledBitmap.CanFreeze)
                        scaledBitmap.Freeze();
                    processImage = scaledBitmap;
                }
                
                BitmapSource writableBitmap = CreateWritableBitmap(processImage);
                
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

        private byte[] TryEncodeWithEncoder(BitmapSource image, System.Windows.Media.Imaging.BitmapEncoder encoder)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        private byte[] TryEncodeWithPngEncoder(BitmapSource image)
        {
            return TryEncodeWithEncoder(image, new System.Windows.Media.Imaging.PngBitmapEncoder());
        }

        private byte[] TryEncodeWithJpegEncoder(BitmapSource image)
        {
            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 95 };
            return TryEncodeWithEncoder(image, encoder);
        }

        private byte[] TryEncodeWithBmpEncoder(BitmapSource image)
        {
            return TryEncodeWithEncoder(image, new System.Windows.Media.Imaging.BmpBitmapEncoder());
        }

        private byte[] TryEncodeWithGdiPlus(BitmapSource image)
        {
            try
            {
                int width = image.PixelWidth;
                int height = image.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];

                image.CopyPixels(pixels, stride, 0);

                using var gdiBitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                System.Drawing.Imaging.BitmapData bmpData = gdiBitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
                gdiBitmap.UnlockBits(bmpData);
                pixels = null;

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
                var image = System.Windows.Clipboard.GetImage();
                if (image != null)
                {
                    if (image.CanFreeze)
                        image.Freeze();
                    return image;
                }
                    
                var dataObject = System.Windows.Clipboard.GetDataObject();
                if (dataObject != null)
                {
                    if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
                    {
                        var bitmap = dataObject.GetData(System.Windows.DataFormats.Bitmap);
                        if (bitmap is System.Drawing.Bitmap gdiBitmap)
                        {
                            IntPtr hBitmap = gdiBitmap.GetHbitmap();
                            try
                            {
                                var result = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                if (result.CanFreeze)
                                    result.Freeze();
                                return result;
                            }
                            finally
                            {
                                DeleteObject(hBitmap);
                            }
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
                            if (bitmapImage.CanFreeze)
                                bitmapImage.Freeze();
                            return bitmapImage;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get clipboard image error: {ex.Message}");
            }
            
            return null;
        }

        private void ProcessImageClipboard(BitmapSource image)
        {
            try
            {
                string imagesFolder = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "images");
                System.IO.Directory.CreateDirectory(imagesFolder);
                
                var imageData = ImageToBytes(image);
                
                if (imageData.Length == 0)
                {
                    return;
                }
                
                string imageHash = ComputeHash(imageData);
                
                if (ShouldProcessImage(imageData, imageHash))
                {
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                    string extension = ".png";
                    string fileName = $"{timestamp}_{imageHash.Substring(0, 8)}{extension}";
                    string relativePath = System.IO.Path.Combine("images", fileName);
                    string fullPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, relativePath);
                    
                    System.IO.File.WriteAllBytes(fullPath, imageData);
                    imageData = null;
                    
                    var item = new ClipboardItem
                    {
                        Type = ClipboardType.Image,
                        ImagePath = relativePath,
                        ImageHash = imageHash,
                        PreviewText = $"{image.PixelWidth}x{image.PixelHeight}",
                        CreatedAt = DateTime.Now
                    };

                    SaveItem(item);
                }
                else
                {
                    imageData = null;
                }

                SchedulePostProcessGc();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Process image clipboard error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();

                _postProcessGcTimer?.Dispose();
                _postProcessGcTimer = null;

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(WndProc);
                    _hwndSource.Dispose();
                    _hwndSource = null;
                }

                _disposed = true;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);
    }
}