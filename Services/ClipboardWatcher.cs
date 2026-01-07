using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace BambooTrans.Services
{
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
                // 延迟到 UI 线程读取（给写入方时间完成）
                _ = Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    // 适配 IM/PDF：多试几次
                    string text = await ClipboardHelper.ReadAnyTextWithRetryAsync(retries: 12, delayMs: 100);
                    if (!string.IsNullOrWhiteSpace(text))
                        TextCopied?.Invoke(text.Trim());
                });
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll")] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
