using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BambooTrans.Services
{
    /// <summary>
    /// 监听系统剪贴板变化（WM_CLIPBOARDUPDATE），当剪贴板有文本时触发事件。
    /// </summary>
    public sealed class ClipboardWatcher : IDisposable
    {
        private readonly Window _window;
        private HwndSource? _source;
        private IntPtr _hwnd = IntPtr.Zero;

        public event Action<string>? TextCopied;

        public ClipboardWatcher(Window window)
        {
            _window = window;
            _window.SourceInitialized += (_, __) =>
            {
                _hwnd = new WindowInteropHelper(_window).Handle;
                _source = HwndSource.FromHwnd(_hwnd);
                _source.AddHook(WndProc);
                AddClipboardFormatListener(_hwnd);
            };
        }

        public void Dispose()
        {
            try
            {
                if (_hwnd != IntPtr.Zero) RemoveClipboardFormatListener(_hwnd);
                if (_source != null) _source.RemoveHook(WndProc);
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_CLIPBOARDUPDATE = 0x031D;
            if (msg == WM_CLIPBOARDUPDATE)
            {
                // 不要在这里做重操作，只尝试拿文本
                try
                {
                    var data = System.Windows.Clipboard.GetDataObject();
                    if (data != null)
                    {
                        if (data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
                        {
                            if (data.GetData(System.Windows.DataFormats.UnicodeText) is string s && !string.IsNullOrWhiteSpace(s))
                            {
                                TextCopied?.Invoke(s);
                            }
                        }
                        else if (data.GetDataPresent(System.Windows.DataFormats.Text))
                        {
                            if (data.GetData(System.Windows.DataFormats.Text) is string s && !string.IsNullOrWhiteSpace(s))
                            {
                                TextCopied?.Invoke(s);
                            }
                        }
                    }
                }
                catch { /* 剪贴板占用时可能抛错，忽略本次 */ }
                handled = false;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
