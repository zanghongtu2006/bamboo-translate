using System;
using System.Windows.Automation;

namespace BambooTrans.Services
{
    public static class SelectionReader
    {
        // 尝试用 UIA 读取选中文本（很多编辑控件、浏览器内 editable 区域都支持）
        public static string? TryGetSelectedText()
        {
            try
            {
                var elem = AutomationElement.FocusedElement;
                if (elem == null) return null;

                if (elem.TryGetCurrentPattern(TextPattern.Pattern, out var p) && p is TextPattern tp)
                {
                    var sels = tp.GetSelection();
                    if (sels != null && sels.Length > 0)
                    {
                        var s = sels[0].GetText(int.MaxValue);
                        if (!string.IsNullOrWhiteSpace(s))
                            return s.Trim();
                    }
                }
            }
            catch { /* 某些进程/控件不支持 UIA */ }

            return null;
        }
    }
}
