using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BambooTrans.Services
{
    /// <summary>
    /// 轮询 GetClipboardSequenceNumber，检测剪贴板变化；变化后延迟读取文本（兼容 HTML/RTF）。
    /// 不依赖 WM_CLIPBOARDUPDATE，适配 Word/微信/Foxit/Notepad++ 等。
    /// </summary>
    public sealed class ClipboardPoller : IDisposable
    {
        private readonly int _intervalMs;
        private CancellationTokenSource? _cts;
        private uint _lastSeq = 0;

        public event Action<string>? TextChanged;

        public ClipboardPoller(int intervalMs = 150)
        {
            _intervalMs = Math.Max(60, intervalMs);
            _lastSeq = GetClipboardSequenceNumber();
        }

        public void Start()
        {
            Stop();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        uint seq = GetClipboardSequenceNumber();
                        if (seq != _lastSeq)
                        {
                            _lastSeq = seq;
                            // 延迟一点，给写入方完成格式化
                            await Task.Delay(120, token);
                            string text = await ClipboardHelper.ReadAnyTextWithRetryAsync(retries: 12, delayMs: 100);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                // 回到 UI 线程触发事件
                                _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                                    TextChanged?.Invoke(text.Trim()));
                            }
                        }
                    }
                    catch { /* ignore */ }

                    try { await Task.Delay(_intervalMs, token); } catch { }
                }
            }, token);
        }

        public void Stop()
        {
            try { _cts?.Cancel(); } catch { }
            _cts = null;
        }

        public void Dispose() => Stop();

        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    }
}
