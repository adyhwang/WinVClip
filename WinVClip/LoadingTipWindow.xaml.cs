using System.Windows;

namespace WinVClip
{
    /// <summary>
    /// 轻量级 Loading 提示弹窗。
    /// 在独立 STA 线程上创建运行，避免主 UI 线程被 OpenClipboard 阻塞时界面卡死。
    /// 生命周期由 ClipboardWin32Helper.ShowClipboardLoading / CloseClipboardLoading 管理。
    /// </summary>
    public partial class LoadingTipWindow : Window
    {
        public LoadingTipWindow()
        {
            InitializeComponent();
        }
    }
}
