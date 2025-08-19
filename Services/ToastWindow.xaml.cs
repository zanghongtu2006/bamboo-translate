using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace BambooTrans.Services
{
    public partial class ToastWindow : Window
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public ToastWindow(string text, int ms = 6000)
        {
            InitializeComponent();
            Txt.Text = text;
            _timer.Interval = TimeSpan.FromMilliseconds(ms);
            _timer.Tick += (_, __) => Close();

            Loaded += (_, __) =>
            {
                // 先完成内容测量，得到最终高度
                Txt.Measure(new Size(400, 320));
                Height = Math.Min(320, Txt.DesiredSize.Height + 24);
                MakeNoActivate(this);     // 不激活窗口（防止抢焦点）
                _timer.Start();
            };

            MouseLeftButtonDown += (_, __) => Close();
        }

        private static void MakeNoActivate(Window w)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public static void ShowNearMouse(string text, int ms = 6000)
        {
            // 1) 取屏幕像素坐标
            if (!GetCursorPos(out POINT p))
            {
                p = new POINT { X = 0, Y = 0 };
            }

            // 2) 创建窗口后，用其 DPI 进行像素→DIP 转换
            var toast = new ToastWindow(text, ms);
            var dpi = VisualTreeHelper.GetDpi(toast); // .DpiScaleX/Y
            double mouseX = p.X / dpi.DpiScaleX;
            double mouseY = p.Y / dpi.DpiScaleY;

            toast.Left = mouseX + 12;
            toast.Top = mouseY + 12;

            // 防止超出屏幕（WorkArea 已是 DIP）
            var wa = SystemParameters.WorkArea;
            if (toast.Left + toast.Width > wa.Right) toast.Left = wa.Right - toast.Width - 12;
            if (toast.Top + toast.Height > wa.Bottom) toast.Top = wa.Bottom - toast.Height - 12;

            toast.Show();
        }

        // --- Win32: 获取鼠标位置（屏幕像素坐标） ---
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        public static void ShowNearWindow(Window anchor, string text, int ms = 6000)
        {
            var toast = new ToastWindow(text, ms);

            // 以锚点窗口（悬浮球）作为基准
            // 显示在锚点右下边 12px 处
            var left = anchor.Left + anchor.Width + 12;
            var top = anchor.Top + 12;

            // 防止超出屏幕
            var wa = SystemParameters.WorkArea;
            if (left + toast.Width > wa.Right) left = anchor.Left - toast.Width - 12;
            if (top + toast.Height > wa.Bottom) top = wa.Bottom - toast.Height - 12;

            toast.Left = left;
            toast.Top = top;
            toast.Show();
        }

    }

}
