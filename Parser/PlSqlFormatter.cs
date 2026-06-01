using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PlSqlAnalyzer.Parser {
  public class FormatOptions {
    public int IndentSize { get; set; } = 4;
    public bool UpperKeywords { get; set; } = true;
    public bool RemoveBlankLines { get; set; } = false;
    public bool RemoveAllComments { get; set; } = false;
  }

  public class PlSqlFormatter {
    static readonly HashSet<string> IncreaseAfter = new(StringComparer.OrdinalIgnoreCase) {
            "BEGIN", "DECLARE", "THEN", "ELSE", "LOOP", "IS", "AS"
        };
    static readonly HashSet<string> DecreaseBefore = new(StringComparer.OrdinalIgnoreCase) {
            "END", "ELSE", "ELSIF", "EXCEPTION", "WHEN"
        };

    static readonly string[] Kws = {
            "SELECT","FROM","WHERE","INSERT","INTO","UPDATE","DELETE","MERGE",
            "PACKAGE","BODY","PROCEDURE","FUNCTION","BEGIN","END","DECLARE",
            "IS","AS","RETURN","IF","THEN","ELSIF","ELSE","LOOP","FOR","WHILE",
            "EXIT","WHEN","CURSOR","OPEN","CLOSE","FETCH","BULK","COLLECT",
            "FORALL","EXCEPTION","RAISE","PRAGMA","TYPE","RECORD","CONSTANT",
            "NULL","TRUE","FALSE","IN","OUT","NOCOPY","DEFAULT","TRIGGER",
            "BEFORE","AFTER","CREATE","OR","REPLACE","COMMIT","ROLLBACK",
            "GROUP","BY","ORDER","HAVING","UNION","INTERSECT","MINUS",
            "JOIN","LEFT","RIGHT","INNER","OUTER","FULL","CROSS","ON",
            "CONNECT","START","WITH","OVER","PARTITION","DISTINCT",
            "AND","NOT","LIKE","BETWEEN","EXISTS","SET","VALUES",
            "EXECUTE","IMMEDIATE","USING","CASE","SAVEPOINT"
        };

    // ── 포맷 ─────────────────────────────────────────────────
    public string Format(string src, FormatOptions opt) {
      if (opt.RemoveAllComments)
        src = RemoveComments(src);

      var lines = src.Split('\n');
      var sb = new StringBuilder();
      int indent = 0;
      bool inBlockCmt = false;

      foreach (var raw in lines) {
        var line = raw.TrimEnd();
        var trimmed = line.Trim();

        if (string.IsNullOrWhiteSpace(trimmed)) {
          if (!opt.RemoveBlankLines)
            sb.AppendLine();
          continue;
        }

        // 블록 주석 그대로 유지
        if (inBlockCmt) {
          sb.AppendLine(new string(' ', indent * opt.IndentSize) + trimmed);
          if (trimmed.Contains("*/"))
            inBlockCmt = false;
          continue;
        }
        if (trimmed.StartsWith("/*") && !trimmed.Contains("*/")) {
          inBlockCmt = true;
        }

        // 라인 주석 그대로 유지
        if (trimmed.StartsWith("--")) {
          sb.AppendLine(new string(' ', indent * opt.IndentSize) + trimmed);
          continue;
        }

        if (opt.UpperKeywords)
          trimmed = UpperKw(trimmed);

        // 감소 먼저 (END, ELSE 등)
        var first = FirstWord(trimmed);
        if (DecreaseBefore.Contains(first))
          indent = Math.Max(0, indent - 1);

        sb.AppendLine(new string(' ', indent * opt.IndentSize) + trimmed);

        // 증가 후 (BEGIN, THEN 등)
        var upper = trimmed.ToUpper();
        if (IncreaseAfter.Contains(first.ToUpper())
            || upper.TrimEnd().EndsWith(" THEN")
            || upper.TrimEnd().EndsWith("\tTHEN")
            || upper.TrimEnd().EndsWith(" IS")
            || upper.TrimEnd().EndsWith(" AS"))
          indent++;

        // END xxx; 는 증가 없음
        if (Regex.IsMatch(upper, @"^\s*END\b"))
          indent = Math.Max(0, indent);
      }
      return sb.ToString();
    }

    // ── 주석 제거 ────────────────────────────────────────────
    public string RemoveComments(string src) {
      var sb = new StringBuilder(src.Length);
      bool inStr = false, inLc = false, inBc = false;

      for (int i = 0; i < src.Length; i++) {
        char c = src[i];
        char nx = i + 1 < src.Length ? src[i + 1] : '\0';

        if (inStr) {
          sb.Append(c);
          if (c == '\'') {
            if (nx == '\'') {
              sb.Append(nx);
              i++;
            }
            else
              inStr = false;
          }
          continue;
        }
        if (inLc) {
          if (c == '\n') {
            inLc = false;
            sb.Append('\n');
          }
          continue;
        }
        if (inBc) {
          if (c == '*' && nx == '/') {
            inBc = false;
            i++;
          }
          else if (c == '\n')
            sb.Append('\n');
          continue;
        }

        if (c == '\'') {
          inStr = true;
          sb.Append(c);
        }
        else if (c == '-' && nx == '-') {
          inLc = true;
          i++;
        }
        else if (c == '/' && nx == '*') {
          inBc = true;
          i++;
        }
        else
          sb.Append(c);
      }

      // 주석 제거 후 빈 줄 정리
      var result = new StringBuilder();
      foreach (var l in sb.ToString().Split('\n'))
        if (!string.IsNullOrWhiteSpace(l))
          result.AppendLine(l.TrimEnd());
      return result.ToString();
    }

    // ── 헬퍼 ─────────────────────────────────────────────────
    string UpperKw(string line) {
      foreach (var kw in Kws)
        line = Regex.Replace(line, $@"\b{kw}\b", kw, RegexOptions.IgnoreCase);
      return line;
    }
    string FirstWord(string s) {
      var m = Regex.Match(s.Trim(), @"^\w+");
      return m.Success ? m.Value : "";
    }
  }
}