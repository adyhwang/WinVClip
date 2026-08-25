using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using QRCoder;
using WinVClip.Services;

namespace WinVClip
{
    public partial class QRCodeWindow : Window
    {
        private readonly string _content;
        private BitmapSource _qrCodeBitmap;

        
        public const int MaxQrBytes = 2331;

        
        public static bool IsContentTooLarge(string content)
        {
            return System.Text.Encoding.UTF8.GetByteCount(content ?? "") > MaxQrBytes;
        }

        public QRCodeWindow(string content)
        {
            InitializeComponent();
            _content = content ?? "";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Loaded += (_, _) =>
            {
                ApplyLocalization();
                GenerateQRCode();
            };
        }

        private void ApplyLocalization()
        {
            TitleText.Text = Loc.Get("QRCode.Window.Title", "二维码");
            ContentText.Text = _content;
            CopyButton.Content = Loc.Get("QRCode.Button.Copy", "复制图片");
            SaveButton.Content = Loc.Get("QRCode.Button.Save", "保存图片");
        }

        private void GenerateQRCode()
        {
            
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(_content);

            if (byteCount > MaxQrBytes)
            {
                var msg = string.Format(
                    Loc.Get("QRCode.Error.TooLarge",
                        "内容过长，超出二维码容量上限（最多 {0} 字节，约 {1} 个汉字），生成失败。\n当前内容：{2} 字节（{3} 字符）"),
                    MaxQrBytes, MaxQrBytes / 3, byteCount, _content.Length);
                MessageBox.Show(msg,
                    Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var generator = new QRCodeGenerator();
                var qrCodeData = generator.CreateQrCode(_content, QRCodeGenerator.ECCLevel.M);
                var qrCode = new QRCode(qrCodeData);

                var qrCodeImage = qrCode.GetGraphic(20,
                    System.Drawing.Color.FromArgb(0, 0, 0),
                    System.Drawing.Color.FromArgb(255, 255, 255),
                    true);

                var bitmap = new BitmapImage();
                using (var ms = new MemoryStream())
                {
                    qrCodeImage.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                _qrCodeBitmap = bitmap;
                QRCodeImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Get("QRCode.Error.Generate", "生成二维码失败: ") + ex.Message,
                    Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_qrCodeBitmap == null) return;

            try
            {
                ClipboardWin32Helper.SetImage(_qrCodeBitmap);
                MessageBox.Show(Loc.Get("QRCode.Message.Copied", "二维码图片已复制到剪贴板"),
                    Loc.Get("Common.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Get("QRCode.Error.Copy", "复制失败: ") + ex.Message,
                    Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_qrCodeBitmap == null) return;

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = Loc.Get("QRCode.SaveFilter", "PNG图片 (*.png)|*.png"),
                    FileName = "qrcode.png",
                    Title = Loc.Get("QRCode.SaveTitle", "保存二维码图片")
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(_qrCodeBitmap));
                    using (var fs = new FileStream(saveDialog.FileName, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                    MessageBox.Show(Loc.Get("QRCode.Message.Saved", "二维码图片已保存"),
                        Loc.Get("Common.Success", "成功"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.Get("QRCode.Error.Save", "保存失败: ") + ex.Message,
                    Loc.Get("Common.Error", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }
    }
}
