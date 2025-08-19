using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BambooTrans.Services
{
    public sealed class Translator
    {
        // 建议复用 HttpClient；禁用其自带 Timeout（用每次请求的 CTS 控制）
        private static readonly HttpClient _http = new HttpClient(new SocketsHttpHandler
        {
            // 连接建立也限时，避免 DNS/建连卡死
            ConnectTimeout = TimeSpan.FromMilliseconds(300),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1)
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        // 默认目标语言
        public string DefaultTargetLang { get; set; } = "ZH-CN";

        /// <summary>
        /// 翻译文本（自动识别源语言 → 指定目标语言）
        /// timeoutMs 默认 500ms；超时会抛 TimeoutException（上层已 try/catch）
        /// </summary>
        public async Task<string> TranslateAsync(string text, string? targetLang = null, int timeoutMs = 500)
        {
            targetLang ??= DefaultTargetLang;

            // --- MyMemory 免费 API ---
            // 文档: https://mymemory.translated.net/doc/spec.php
            // 注意: 免费接口质量/速率有限，开发期够用
            var q = Uri.EscapeDataString(text);
            var to = MapLang(targetLang);
            var url = $"https://api.mymemory.translated.net/get?q={q}&langpair=en|{to}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                using var rsp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                rsp.EnsureSuccessStatusCode();

                // 读取内容也受超时控制
                using var stream = await rsp.Content.ReadAsStreamAsync(cts.Token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

                if (doc.RootElement.TryGetProperty("responseData", out var data) &&
                    data.TryGetProperty("translatedText", out var t))
                {
                    var val = t.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }

                return "[翻译失败] 未返回有效结果";
            }
            catch (OperationCanceledException)
            {
                // 统一转成 TimeoutException，让上层提示“超时”
                throw new TimeoutException($"翻译超时（>{timeoutMs}ms）");
            }
            catch (Exception ex)
            {
                return "[翻译失败] " + ex.Message;
            }
        }

        private static string MapLang(string lang)
        {
            var up = lang.ToUpperInvariant();
            return up switch
            {
                "ZH" or "ZH-CN" or "ZH_CN" => "ZH-CN",
                "EN" or "EN-US" or "EN_GB" => "EN",
                "JA" or "JP" => "JA",
                "DE" => "DE",
                _ => up
            };
        }
    }
}
