using System;
using System.Collections.Generic;
using System.Text;

using ICSharpCode.AvalonEdit.Folding;

namespace PlSqlAnalyzer.Strategy {
  // ── BEGIN/END Folding strategy ────────────────────────────
  public class PlSqlFolding {
    static readonly System.Text.RegularExpressions.Regex
        RxBegin = new(@"\b(BEGIN|IF|LOOP|CASE|DECLARE)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase),
        RxEnd = new(@"\bEND\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public void UpdateFoldings(FoldingManager? manager,
                               ICSharpCode.AvalonEdit.Document.TextDocument? doc) {
      // null 또는 이미 해제된 매니저면 건너뜀
      if (manager == null || doc == null)
        return;

      try {
        var foldings = new System.Collections.Generic.List<NewFolding>();
        var stack = new System.Collections.Generic.Stack<(int offset, string label)>();

        foreach (var line in doc.Lines) {
          var txt = doc.GetText(line.Offset, line.Length).Trim();
          int ci = txt.IndexOf("--", StringComparison.Ordinal);
          if (ci >= 0)
            txt = txt[..ci].Trim();
          if (string.IsNullOrEmpty(txt))
            continue;

          if (RxBegin.IsMatch(txt))
            stack.Push((line.Offset, txt[..Math.Min(40, txt.Length)]));

          if (RxEnd.IsMatch(txt) && stack.Count > 0) {
            var (startOff, label) = stack.Pop();
            if (line.EndOffset > startOff + 10)
              foldings.Add(new NewFolding(startOff, line.EndOffset) { Name = label });
          }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));

        // 매니저가 살아있는지 한 번 더 확인 후 업데이트
        if (manager.AllFoldings != null)
          manager.UpdateFoldings(foldings, -1);
      }
      catch { /* 폴딩 오류는 무시 */ }
    }
  }
}
