using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;

namespace BambooTrans.Services
{
    public static class ClipboardHelper
    {
        // ---- SendInput: Ctrl+C ----
        public static void SimulateCopy()
        {
            var inputs = new INPUT[4];
            inputs[0] = KeyDown(0x11); // Ctrl
            inputs[1] = KeyDown(0x43); // 'C'
            inputs[2] = KeyUp(0x43);
            inputs[3] = KeyUp(0x11);
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // ---- SendInput: Ctrl+Insert（兜底）----
        public static void SimulateCopyCtrlInsert()
        {
            var inputs = new INPUT[4];
            inputs[0] = KeyDown(0x11); // Ctrl
            inputs[1] = KeyDown(0x2D); // Insert
            inputs[2] = KeyUp(0x2D);
            inputs[3] = KeyUp(0x11);
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // 复制并等待“剪贴板序号变化”，变化后读取文本（兼容 HTML/RTF）
        public static async Task<(bool changed, string text)> CopyThenReadAsync(
            int changeTimeoutMs = 700, int retries = 12, int delayMs = 120)
        {
            uint before = GetClipboardSequenceNumber();
            SimulateCopy();

            var start = Environment.TickCount64;
            while ((Environment.TickCount64 - start) < changeTimeoutMs)
            {
                uint now = GetClipboardSequenceNumber();
                if (now != before)
                {
                    string txt = await ReadAnyTextWithRetryAsync(retries, delayMs);
                    return (true, (txt ?? string.Empty).Trim());
                }
                await Task.Delay(40);
            }
            return (false, string.Empty);
        }

        // 读取任何可用文本：UnicodeText/Text/HTML/RTF
        public static async Task<string> ReadAnyTextWithRetryAsync(int retries = 12, int delayMs = 100)
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
                        if (data.GetDataPresent(DataFormats.Rtf))
                        {
                            if (data.GetData(DataFormats.Rtf) is string rtf)
                            {
                                var s = CfRtfToPlain(rtf);
                                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
                            }
                        }
                    }
                }
                catch { /* 占用，稍后重试 */ }

                await Task.Delay(delayMs);
            }
            return string.Empty;
        }

        // ---- 辅助：HTML → 纯文本（粗略）----
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

        // ---- 辅助：RTF → 纯文本（用 WPF RichTextBox 解析）----
        private static string CfRtfToPlain(string rtf)
        {
            try
            {
                var rtb = new RichTextBox(); // 不必放到可见树
                var range = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);
                // RTF 是 8-bit 文本，尝试 UTF8/ASCII 读取
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(rtf)))
                {
                    try { range.Load(ms, DataFormats.Rtf); }
                    catch
                    {
                        ms.Position = 0;
                        var bytes = Encoding.ASCII.GetBytes(rtf);
                        using var ms2 = new MemoryStream(bytes);
                        range.Load(ms2, DataFormats.Rtf);
                    }
                }
                return range.Text ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        // ---- Win32 ----
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion U; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        private static INPUT KeyDown(ushort vk) => new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 } } };
        private static INPUT KeyUp(ushort vk) => new INPUT { type = 1, U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = 0x0002 } } };
    }
}
