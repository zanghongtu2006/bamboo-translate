using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using BambooTrans.Services;

namespace BambooTrans
{
    public partial class MainWindow : Window
    {
        private readonly Translator _translator = new Translator();

        // 钩子 + 剪贴板“序号轮询”监视器
        private GlobalMouseHook? _mouseHook;
        private ClipboardPoller? _clipPoller;

        // 行为开关
        private bool _smartSelectEnabled = true;   // 划词松开自动翻译
        private bool _clipAutoEnabled = true;      // 剪贴板变化自动翻译（你手动 Ctrl+C 时触发）
        private bool _useWhitelist = false;        // 默认关闭白名单 => 全局应用生效

        // 去抖/去重
        private DateTime _lastTrigger = DateTime.MinValue;
        private string _lastHash = "";
        private DateTime _lastHashAt = DateTime.MinValue;
        private static readonly TimeSpan DuplicateSuppress = TimeSpan.FromSeconds(2);

        // 模式间合并抑制：划词触发后 800ms 内忽略相同的“剪贴板变化”触发
        private DateTime _lastSelectAt = DateTime.MinValue;

        // 进程白名单（可在菜单切换是否启用）
        private readonly string[] _procWhitelist = {
            "chrome","msedge","firefox","notepad","code","winword","excel","powerpnt",
            "wps","wpswriter","wpspdf",
            "acrord32","acrord64","acrobat","foxit","foxitreader","foxitpdfreader",
            "sumatrapdf","pdfxedit","nitropdf","okular","pdfviewer","edgewebview",
            "wechat","wecom","wxwork","whatsapp","telegram","slack","discord","skype","teams","signal"
        };

        // 进程黑名单（避免自家/系统）
        private readonly string[] _procBlocklist = {
            "bambootrans","explorer","taskmgr","systemsettings","searchapp",
            "applicationframehost","shellexperiencehost","textinputhost","devenv"
        };

        public MainWindow()
        {
            InitializeComponent();

            // 初始位置：右下角留一点边距
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 20;
            Top = wa.Bottom - Height - 100;

            Loaded += (_, __) =>
            {
                // ① 鼠标钩子：左键松开 → 划词翻译
                _mouseHook = new GlobalMouseHook();
                _mouseHook.LeftButtonUp += () =>
                    _ = Dispatcher.InvokeAsync(async () => await TrySmartTranslateAsync());

                // ② 剪贴板“序号轮询”：任何应用手动 Ctrl+C → 自动翻译
                //    *不依赖 WM_CLIPBOARDUPDATE，专治 Word/微信/Foxit 这类“延迟/格式化写入”
                _clipPoller = new ClipboardPoller(intervalMs: 150);
                _clipPoller.TextChanged += async (txt) =>
                {
                    if (!_clipAutoEnabled) return;
                    if (string.IsNullOrWhiteSpace(txt)) return;

                    // 与“划词”合并：800ms 内同文案忽略
                    if ((DateTime.UtcNow - _lastSelectAt).TotalMilliseconds < 800)
                    {
                        if (Sha1(txt) == _lastHash) return;
                    }
                    await TranslateAndToastAsync(txt.Trim());
                };
                _clipPoller.Start();
            };

            Closed += (_, __) =>
            {
                _mouseHook?.Dispose();
                _clipPoller?.Dispose();
            };
        }

        private async Task TrySmartTranslateAsync()
        {
            try
            {
                if (!_smartSelectEnabled) return;

                // 去抖：120ms 内不重复触发
                if ((DateTime.UtcNow - _lastTrigger).TotalMilliseconds < 120) return;
                _lastTrigger = DateTime.UtcNow;

                // 前台进程过滤
                string? proc = ForegroundProcessName();
                if (proc != null && IsInBlocklist(proc)) return;
                if (_useWhitelist && (proc == null || !IsInWhitelist(proc))) return;

                // 等 60ms 确保选区完成
                await Task.Delay(60);

                // 针对 PDF/IM 适当拉长等待
                int extra = 0;
                if (!string.IsNullOrEmpty(proc))
                {
                    var p = proc.ToLowerInvariant();
                    if (p.Contains("acro") || p.Contains("foxit") || p.Contains("pdf") || p.Contains("xedit") || p.Contains("sumatra"))
                        extra = 300;
                    if (p.Contains("wechat") || p.Contains("wxwork") || p.Contains("wecom") ||
                        p.Contains("whatsapp") || p.Contains("telegram") || p.Contains("slack") ||
                        p.Contains("discord") || p.Contains("teams") || p.Contains("skype") || p.Contains("signal"))
                        extra = Math.Max(extra, 200);
                }

                // ① Ctrl+C → 等剪贴板序号变化 → 读取（含 HTML/RTF）
                var (changed, text) = await ClipboardHelper.CopyThenReadAsync(
                    changeTimeoutMs: 800 + extra,
                    retries: (extra > 0 ? 16 : 12),
                    delayMs: 120);

                // ② 未变化 → Ctrl+Insert 再试一轮
                if (!changed)
                {
                    ClipboardHelper.SimulateCopyCtrlInsert();
                    (changed, text) = await ClipboardHelper.CopyThenReadAsync(
                        changeTimeoutMs: 800 + extra,
                        retries: (extra > 0 ? 16 : 12),
                        delayMs: 120);
                }

                // ③ 仍未变化 → UIA 直接读“选中文本”
                if (!changed)
                {
                    var uia = SelectionReader.TryGetSelectedText();
                    if (!string.IsNullOrWhiteSpace(uia))
                        text = uia.Trim();
                    else
                        return; // 放弃（不污染旧剪贴板）
                }

                _lastSelectAt = DateTime.UtcNow;

                if (string.IsNullOrWhiteSpace(text)) return;
                await TranslateAndToastAsync(text.Trim());
            }
            catch (Exception ex)
            {
                ToastWindow.ShowNearWindow(this, "内部错误：" + ex.Message, 1800);
            }
        }

        private async Task TranslateAndToastAsync(string text)
        {
            if (text.Length < 2 || text.Length > 5000) return; // 长度过滤

            // 去重：同一段文本 2 秒内不重复翻译
            string hash = Sha1(text);
            var now = DateTime.UtcNow;
            if (hash == _lastHash && (now - _lastHashAt) < DuplicateSuppress) return;
            _lastHash = hash;
            _lastHashAt = now;

            try
            {
                ToastWindow.ShowNearWindow(this, "翻译中…", 900);
                var result = await _translator.TranslateAsync(text, _translator.DefaultTargetLang, timeoutMs: 500);
                ToastWindow.ShowNearWindow(this, result);
            }
            catch (Exception ex)
            {
                ToastWindow.ShowNearWindow(this, "翻译失败：" + ex.Message, 1600);
            }
        }

        // 左键拖拽悬浮球
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // 右键菜单
        private void Border_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var menu = new System.Windows.Controls.ContextMenu();

            // 模式开关
            var itemWL = new System.Windows.Controls.MenuItem
            {
                Header = _useWhitelist ? "仅白名单进程触发：开" : "仅白名单进程触发：关",
                IsCheckable = true,
                IsChecked = _useWhitelist
            };
            itemWL.Click += (_, __) =>
            {
                _useWhitelist = !_useWhitelist;
                itemWL.Header = _useWhitelist ? "仅白名单进程触发：开" : "仅白名单进程触发：关";
                itemWL.IsChecked = _useWhitelist;
            };

            var itemSmart = new System.Windows.Controls.MenuItem
            {
                Header = _smartSelectEnabled ? "智能划词翻译：开" : "智能划词翻译：关",
                IsCheckable = true,
                IsChecked = _smartSelectEnabled
            };
            itemSmart.Click += (_, __) =>
            {
                _smartSelectEnabled = !_smartSelectEnabled;
                itemSmart.Header = _smartSelectEnabled ? "智能划词翻译：开" : "智能划词翻译：关";
                itemSmart.IsChecked = _smartSelectEnabled;
            };

            var itemClip = new System.Windows.Controls.MenuItem
            {
                Header = _clipAutoEnabled ? "剪贴板自动翻译：开" : "剪贴板自动翻译：关",
                IsCheckable = true,
                IsChecked = _clipAutoEnabled
            };
            itemClip.Click += (_, __) =>
            {
                _clipAutoEnabled = !_clipAutoEnabled;
                itemClip.Header = _clipAutoEnabled ? "剪贴板自动翻译：开" : "剪贴板自动翻译：关";
                itemClip.IsChecked = _clipAutoEnabled;
            };

            menu.Items.Add(itemWL);
            menu.Items.Add(itemSmart);
            menu.Items.Add(itemClip);
            menu.Items.Add(new System.Windows.Controls.Separator());

            // 目标语言
            var itemAutoZh = new System.Windows.Controls.MenuItem { Header = "目标语言：中文（默认）" };
            itemAutoZh.Click += (_, __) => _translator.DefaultTargetLang = "ZH-CN";
            var itemAutoEn = new System.Windows.Controls.MenuItem { Header = "目标语言：英文" };
            itemAutoEn.Click += (_, __) => _translator.DefaultTargetLang = "EN";
            menu.Items.Add(itemAutoZh);
            menu.Items.Add(itemAutoEn);
            menu.Items.Add(new System.Windows.Controls.Separator());

            // 测试翻译（当前剪贴板）
            var itemTest = new System.Windows.Controls.MenuItem { Header = "测试翻译（剪贴板）" };
            itemTest.Click += async (_, __) =>
            {
                var clip = await ClipboardHelper.ReadAnyTextWithRetryAsync();
                var text = string.IsNullOrWhiteSpace(clip) ? "你好，世界！" : clip.Trim();
                await TranslateAndToastAsync(text);
            };
            menu.Items.Add(itemTest);
            menu.Items.Add(new System.Windows.Controls.Separator());

            // 退出
            var itemExit = new System.Windows.Controls.MenuItem { Header = "退出" };
            itemExit.Click += (_, __) => Application.Current.Shutdown();
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        }

        private static string Sha1(string s)
        {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes);
        }

        private static string? ForegroundProcessName()
        {
            try
            {
                var hwnd = Win32.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;
                _ = Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                var p = Process.GetProcessById((int)pid);
                return p.ProcessName.ToLowerInvariant();
            }
            catch { return null; }
        }

        private bool IsInWhitelist(string proc)
        {
            foreach (var w in _procWhitelist)
                if (proc.Contains(w)) return true;
            return false;
        }

        private bool IsInBlocklist(string proc)
        {
            foreach (var b in _procBlocklist)
                if (proc.Contains(b)) return true;
            return false;
        }
    }
}
