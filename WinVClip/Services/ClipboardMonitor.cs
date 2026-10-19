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

            // 忽略窗口：前台窗口标题或进程名命中关键词时不记录（CopyQ ignore windows 同类功能）
            if (IsForegroundWindowIgnored())
                return;

            ScheduleDebouncedProcess();
        }

        private bool IsForegroundWindowIgnored()
        {
            try
            {
                var keywords = _settingsService.Settings.IgnoredWindowKeywords;
                if (keywords == null || keywords.Count == 0)
                    return false;

                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                    return false;

                string windowTitle = GetWindowTitle(hwnd);
                string processName = GetWindowProcessName(hwnd);
                if (string.IsNullOrEmpty(windowTitle) && string.IsNullOrEmpty(processName))
                    return false;

                foreach (var keyword in keywords)
                {
                    var k = keyword?.Trim();
                    if (string.IsNullOrEmpty(k))
                        continue;

                    if (!string.IsNullOrEmpty(windowTitle) && windowTitle.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    if (!string.IsNullOrEmpty(processName) && processName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var buf = new StringBuilder(512);
            return GetWindowText(hwnd, buf, buf.Capacity) > 0 ? buf.ToString() : string.Empty;
        }

        private static string GetWindowProcessName(IntPtr hwnd)
        {
            if (GetWindowThreadProcessId(hwnd, out uint pid) == 0 || pid == 0)
                return string.Empty;

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero)
                return string.Empty;

            try
            {
                var buf = new StringBuilder(1024);
                int size = buf.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, buf, ref size) && size > 0)
                    return System.IO.Path.GetFileName(buf.ToString(0, size));
                return string.Empty;
            }
            finally
            {
                CloseHandle(hProcess);
            }
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
            catch (Exception ex)
            {
                App.LogCrash("ClipboardMonitor.Process", ex);
            }
        }

        private void UpdateLastClipboardState()
        {
            _lastContentSignatures.Clear();

            try
            {
                var snapshot = ReadClipboardSnapshot();
                if (snapshot == null)
                    return;

                if (snapshot.HasRichText && snapshot.HasText &&
                    !string.IsNullOrWhiteSpace(snapshot.Text) && !string.IsNullOrWhiteSpace(snapshot.RichContent))
                {
                    _lastContentSignatures[ClipboardType.RichText] =
                        ComputeSignature(ClipboardType.RichText, snapshot.Text + snapshot.RichContent);
                }
                else if (snapshot.HasText)
                {
                    _lastContentSignatures[ClipboardType.Text] =
                        ComputeSignature(ClipboardType.Text, snapshot.Text);
                }

                if (snapshot.Image != null)
                {
                    var image = snapshot.Image;
                    _lastContentSignatures[ClipboardType.Image] =
                        ComputeSignature(ClipboardType.Image, $"{image.PixelWidth}x{image.PixelHeight}_{image.Format}");
                }

                if (snapshot.FilePaths.Length > 0)
                {
                    var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", NormalizeFilePaths(snapshot.FilePaths))));
                    _lastContentSignatures[ClipboardType.FileList] = ComputeSignature(ClipboardType.FileList, hash);
                }
            }
            catch (Exception ex)
            {
                App.LogCrash("ClipboardMonitor.UpdateState", ex);
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

        private volatile bool _isIgnoringChange = false;

        /// <summary>粘贴写入剪贴板的时间戳（0 表示当前没有待处理的粘贴操作）。</summary>
        private int _pasteOperationTimestamp;
        private const int PasteOperationTimeoutMs = 1000;
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
            Interlocked.Exchange(ref _pasteOperationTimestamp, Environment.TickCount);
        }

        public void SetLastPasteHashText(string text)
        {
            _lastPasteHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
        }

        public void SetLastPasteHashFiles(List<string> filePaths)
        {
            _lastPasteHash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", NormalizeFilePaths(filePaths))));
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
            int timestamp = Interlocked.Exchange(ref _pasteOperationTimestamp, 0);
            if (timestamp == 0)
                return false;

            // 超过超时仍未等到剪贴板更新，视为本次写入未生效，避免吞掉用户后续的真实复制
            return unchecked(Environment.TickCount - timestamp) <= PasteOperationTimeoutMs;
        }

        /// <summary>
        /// 单次剪贴板更新的内容快照：一次读取全部所需格式，
        /// 供类型判定、去重与落库复用，避免在监听链路中反复开合剪贴板。
        /// </summary>
        private sealed class ClipboardSnapshot
        {
            public bool HasText;
            public bool HasImage;
            public bool HasFileDrop;
            public bool HasRichText;
            public string Text;
            public string Html;
            public string Rtf;
            public string Csv;
            public string[] FilePaths = Array.Empty<string>();
            public BitmapSource Image;

            public bool HasAny => HasText || HasImage || HasFileDrop || HasRichText;
            public string RichFormat => Html != null ? "HTML" : Rtf != null ? "RTF" : null;
            public string RichContent => Html ?? Rtf;
        }

        /// <summary>读取一次剪贴板生成内容快照；内容不可用时返回 null。</summary>
        private ClipboardSnapshot ReadClipboardSnapshot()
        {
            try
            {
                var dataObject = ClipboardWin32Helper.GetDataObject();
                if (dataObject == null)
                    return null;

                var snapshot = new ClipboardSnapshot
                {
                    HasText = dataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText) ||
                              dataObject.GetDataPresent(System.Windows.DataFormats.Text),
                    HasFileDrop = dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop),
                    HasImage = dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap) ||
                               dataObject.GetDataPresent(System.Windows.DataFormats.Dib),
                    HasRichText = dataObject.GetDataPresent(System.Windows.DataFormats.Html) ||
                                  dataObject.GetDataPresent(System.Windows.DataFormats.Rtf)
                };

                if (snapshot.HasText)
                    snapshot.Text = ClipboardWin32Helper.GetText();

                if (dataObject.GetDataPresent(System.Windows.DataFormats.Html))
                    snapshot.Html = dataObject.GetData(System.Windows.DataFormats.Html) as string;
                else if (dataObject.GetDataPresent(System.Windows.DataFormats.Rtf))
                    snapshot.Rtf = dataObject.GetData(System.Windows.DataFormats.Rtf) as string;

                if (dataObject.GetDataPresent(System.Windows.DataFormats.CommaSeparatedValue))
                    snapshot.Csv = dataObject.GetData(System.Windows.DataFormats.CommaSeparatedValue) as string;

                if (snapshot.HasFileDrop && dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] fileList)
                {
                    snapshot.FilePaths = fileList
                        .Where(f => File.Exists(f) || Directory.Exists(f))
                        .ToArray();
                }

                if (snapshot.HasImage)
                    snapshot.Image = GetClipboardImage(dataObject);

                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        private void ProcessClipboard()
        {
            var snapshot = ReadClipboardSnapshot();
            if (snapshot == null || !snapshot.HasAny)
                return;

            var settings = _settingsService.Settings;

            if (snapshot.FilePaths.Length > 0 && settings.CaptureFiles)
            {
                ProcessFileListClipboard(snapshot);
                return;
            }

            if (snapshot.HasRichText && snapshot.HasText && settings.CaptureText &&
                !string.IsNullOrWhiteSpace(snapshot.Text) && !string.IsNullOrWhiteSpace(snapshot.RichContent))
            {
                ProcessRichTextClipboard(snapshot);
                return;
            }

            if (snapshot.HasImage && settings.CaptureImages)
            {
                ProcessImageClipboard(snapshot.Image);
                return;
            }

            if (snapshot.HasText && settings.CaptureText && ShouldProcessText(snapshot.Text))
            {
                SaveItem(new ClipboardItem
                {
                    Type = ClipboardType.Text,
                    Content = snapshot.Text,
                    PreviewText = BuildTextPreview(snapshot.Text),
                    CreatedAt = DateTime.Now
                });
            }
        }

        /// <summary>处理文件与目录列表，签名命中时不记录。</summary>
        private void ProcessFileListClipboard(ClipboardSnapshot snapshot)
        {
            var filePaths = snapshot.FilePaths.ToList();
            if (!ShouldProcessFileList(filePaths))
                return;

            var item = new ClipboardItem
            {
                Type = ClipboardType.FileList,
                FilePaths = filePaths,
                Content = string.Join("\n", NormalizeFilePaths(filePaths)),
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

        /// <summary>处理富文本：命中签名或重复时不记录；剪贴板同时含图片时先编码再落库，编码在后台线程完成。</summary>
        private void ProcessRichTextClipboard(ClipboardSnapshot snapshot)
        {
            if (!ShouldProcessRichText(snapshot.Text, snapshot.RichContent, snapshot.RichFormat))
                return;

            if (snapshot.Image == null)
            {
                SaveRichTextItem(snapshot, null, null);
                return;
            }

            EncodeImageInBackground(snapshot.Image, imageData =>
            {
                string imagePath = null;
                string imageHash = null;

                if (imageData.Length > 0)
                {
                    imageHash = ComputeHash(imageData);
                    imagePath = SaveImageFile(imageData, imageHash);
                }

                SaveRichTextItem(snapshot, imagePath, imageHash);
                SchedulePostProcessGc();
            });
        }

        private void SaveRichTextItem(ClipboardSnapshot snapshot, string imagePath, string imageHash)
        {
            SaveItem(new ClipboardItem
            {
                Type = ClipboardType.RichText,
                Content = snapshot.Text,
                RichContent = snapshot.RichContent,
                RichFormat = snapshot.RichFormat,
                CsvContent = snapshot.Csv,
                ImagePath = imagePath,
                ImageHash = imageHash,
                PreviewText = BuildTextPreview(snapshot.Text ?? ""),
                CreatedAt = DateTime.Now
            });
        }

        /// <summary>后台线程完成图片编码后回到 UI 线程执行后续落库，避免 PNG 压缩阻塞主线程。</summary>
        private void EncodeImageInBackground(BitmapSource image, Action<byte[]> onEncoded)
        {
            Task.Run(() => ImageToBytes(image)).ContinueWith(task =>
            {
                byte[] imageData = task.IsCompletedSuccessfully ? task.Result : Array.Empty<byte>();
                _dispatcher.BeginInvoke(() => onEncoded(imageData));
            });
        }

        /// <summary>将编码后的图片写入 images 目录，返回相对路径。</summary>
        private static string SaveImageFile(byte[] imageData, string imageHash)
        {
            string imagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");
            Directory.CreateDirectory(imagesFolder);

            string fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{imageHash.Substring(0, 8)}.png";
            string relativePath = Path.Combine("images", fileName);
            File.WriteAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath), imageData);
            return relativePath;
        }

        /// <summary>文件路径排序（不区分大小写），用于生成稳定的内容顺序。</summary>
        private static List<string> NormalizeFilePaths(IEnumerable<string> filePaths)
            => filePaths.OrderBy(p => p.ToLowerInvariant()).ToList();

        /// <summary>列表预览文本：超过 100 字符时截断。</summary>
        private static string BuildTextPreview(string text)
            => text.Length > 100 ? text.Substring(0, 100) : text;

        private bool ShouldProcessText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var signature = ComputeSignature(ClipboardType.Text, text);

            if (_lastContentSignatures.TryGetValue(ClipboardType.Text, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.TextExistsInDatabase(text))
            {

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

            var sortedPaths = NormalizeFilePaths(filePaths);
            var content = string.Join("\n", sortedPaths);
            var hash = ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join("|", sortedPaths)));
            var signature = ComputeSignature(ClipboardType.FileList, hash);

            if (_lastContentSignatures.TryGetValue(ClipboardType.FileList, out var lastSig) && signature == lastSig)
                return false;

            if (_settingsService.Settings.RemoveDuplicates && _databaseService.FileListExistsByPaths(filePaths, out var matchingContent))
            {
                if (!string.IsNullOrEmpty(matchingContent))
                {

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

                if (source.Format != System.Windows.Media.PixelFormats.Bgr32 &&
                    source.Format != System.Windows.Media.PixelFormats.Pbgra32)
                {

                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = System.Windows.Media.PixelFormats.Bgr32;
                    converted.EndInit();

                    converted.Freeze();
                    return converted;
                }

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

        /// <summary>
        /// 从已读取的剪贴板数据对象中提取位图，避免为取图再次开合剪贴板。
        /// 依次尝试 WPF 位图、GDI 位图与 DIB 字节流；均无法解析时退回一次性读取以保证兼容性。
        /// </summary>
        private static BitmapSource GetClipboardImage(IDataObject dataObject)
        {
            try
            {
                if (dataObject.GetDataPresent(System.Windows.DataFormats.Bitmap))
                {
                    var bitmap = dataObject.GetData(System.Windows.DataFormats.Bitmap);

                    if (bitmap is BitmapSource source)
                    {
                        if (source.CanFreeze)
                            source.Freeze();
                        return source;
                    }

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

                if (dataObject.GetDataPresent(System.Windows.DataFormats.Dib) &&
                    dataObject.GetData(System.Windows.DataFormats.Dib) is byte[] dibBytes)
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

                var image = ClipboardWin32Helper.GetImage();
                if (image != null && image.CanFreeze)
                    image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                App.LogCrash("ClipboardMonitor.GetImage", ex);
            }

            return null;
        }

        /// <summary>处理图片：编码在后台线程完成，回到 UI 线程后按哈希去重并落库。</summary>
        private void ProcessImageClipboard(BitmapSource image)
        {
            if (image == null)
                return;

            EncodeImageInBackground(image, imageData =>
            {
                if (imageData.Length == 0)
                    return;

                string imageHash = ComputeHash(imageData);

                if (ShouldProcessImage(imageData, imageHash))
                {
                    SaveItem(new ClipboardItem
                    {
                        Type = ClipboardType.Image,
                        ImagePath = SaveImageFile(imageData, imageHash),
                        ImageHash = imageHash,
                        PreviewText = $"{image.PixelWidth}x{image.PixelHeight}",
                        CreatedAt = DateTime.Now
                    });
                }

                SchedulePostProcessGc();
            });
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);
    }
}
