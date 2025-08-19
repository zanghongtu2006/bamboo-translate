using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace BambooTrans.Services
{
    public static class ClipboardHelper
    {
        // -------- SendInput: 模拟 Ctrl+C ----------
        public static void SimulateCopy()
        {
            var inputs = new INPUT[4];
            inputs[0] = KeyDown(VK_CONTROL);
            inputs[1] = KeyDown(0x43); // 'C'
            inputs[2] = KeyUp(0x43);
            inputs[3] = KeyUp(VK_CONTROL);
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // 复制后等待剪贴板“确实改变”，再读取文本
        public static async Task<(bool changed, string text)> CopyThenReadAsync(
            int changeTimeoutMs = 600, int retries = 10, int delayMs = 100)
        {
            uint before = GetClipboardSequenceNumber();
            SimulateCopy();

            var sw = Stopwatch.StartNew();
            // 轮询剪贴板序号是否变化
            while (sw.ElapsedMilliseconds < changeTimeoutMs)
            {
                uint now = GetClipboardSequenceNumber();
                if (now != before)
                {
                    // 真的变了，再去读文本（带重试，兼容 PDF 慢写）
                    string txt = await ReadAnyTextWithRetryAsync(retries, delayMs);
                    return (true, (txt ?? string.Empty).Trim());
                }
                await Task.Delay(40);
            }
            // 超时：没有变化（说明复制失败/被拦/未选中文本）
            return (false, string.Empty);
        }

        // 读取任何可用文本（Text/UnicodeText/HTML）
        public static async Task<string> ReadAnyTextWithRetryAsync(int retries = 10, int delayMs = 120)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        var t = Clipboard.GetText();
                        if (!string.IsNullOrWhiteSpace(t)) return t.Trim();
                    }

                    var data = Clipboard.GetDataObject();
                    if (data != null)
                    {
                        if (data.GetDataPresent(DataFormats.UnicodeText))
                        {
                            if (data.GetData(DataFormats.UnicodeText) is string s && !string.IsNullOrWhiteSpace(s))
                                return s.Trim();
                        }
                        if (data.GetDataPresent(DataFormats.Text))
                        {
                            if (data.GetData(DataFormats.Text) is string s && !string.IsNullOrWhiteSpace(s))
                                return s.Trim();
                        }
                        if (data.GetDataPresent(DataFormats.Html))
                        {
                            if (data.GetData(DataFormats.Html) is string html)
                            {
                                var s = CfHtmlToPlain(html);
                                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                            }
                        }
                    }
                }
                catch { /* 占用中，稍后重试 */ }

                await Task.Delay(delayMs);
            }
            return string.Empty;
        }

        // --- CF_HTML 粗略转纯文本 ---
        private static string CfHtmlToPlain(string? cfhtml)
        {
            if (string.IsNullOrEmpty(cfhtml)) return string.Empty;
            int idx = cfhtml.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
            string html = idx >= 0 ? cfhtml.Substring(idx) : cfhtml;
            html = System.Text.RegularExpressions.Regex.Replace(html, "<script.*?</script>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, "<style.*?</style>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            html = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", " ");
            html = System.Net.WebUtility.HtmlDecode(html);
            return System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ").Trim();
        }

        // ---------- Win32 低层 ----------
        private const ushort VK_CONTROL = 0x11;

        private static INPUT KeyDown(ushort vk) => new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } } };
        private static INPUT KeyUp(ushort vk) => new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    }
}
