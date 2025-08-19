using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BambooTrans.Services
{
    [Flags]
    public enum Modifiers : uint
    {
        MOD_NONE = 0x0000,
        MOD_ALT = 0x0001,
        MOD_CTRL = 0x0002,
        MOD_SHIFT = 0x0004,
        MOD_WIN = 0x0008
    }

    public sealed class HotkeyManager : IDisposable
    {
        private readonly Window _window;
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource? _source;
        private readonly int _id = 0x3711;
        private Action? _callback;

        public HotkeyManager(Window window)
        {
            _window = window;
            _window.SourceInitialized += (s, e) =>
            {
                _hwnd = new WindowInteropHelper(_window).Handle;
                _source = HwndSource.FromHwnd(_hwnd);
                _source.AddHook(WndProc);
            };
        }

        /// <summary>
        /// 注册全局热键（纯 Win32，vk 为虚拟键码，比如 Q=0x51, E=0x45）
        /// </summary>
        public void Register(Modifiers modifiers, uint vk, Action callback)
        {
            _callback = callback;
            if (!RegisterHotKey(_hwnd, _id, (uint)modifiers, vk))
                throw new InvalidOperationException("注册热键失败，可能与系统快捷键冲突。请更换组合键。");
        }

        public void Dispose()
        {
            try { UnregisterHotKey(_hwnd, _id); } catch { }
            if (_source != null) _source.RemoveHook(WndProc);
        }

        private const int WM_HOTKEY = 0x0312;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                _callback?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
