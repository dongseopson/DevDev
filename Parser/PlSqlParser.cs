using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using PlSqlAnalyzer.Models;

namespace PlSqlAnalyzer.Parser {
  public class PlSqlParser {
    // ── Built-ins / Keywords (호출 탐지 제외 목록) ───────────
    static readonly HashSet<string> Builtins = new(StringComparer.OrdinalIgnoreCase) {
            "NVL","NVL2","DECODE","COALESCE","NULLIF","SUBSTR","SUBSTRB","INSTR",
            "INSTRB","LENGTH","LENGTHB","UPPER","LOWER","INITCAP","TRIM","LTRIM",
            "RTRIM","LPAD","RPAD","REPLACE","TRANSLATE","CONCAT","TO_CHAR",
            "TO_DATE","TO_NUMBER","TO_TIMESTAMP","TO_CLOB","TO_BLOB","SYSDATE",
            "SYSTIMESTAMP","CURRENT_DATE","CURRENT_TIMESTAMP","TRUNC","ROUND",
            "FLOOR","CEIL","ABS","MOD","POWER","SQRT","SIGN","COUNT","SUM","AVG",
            "MAX","MIN","STDDEV","VARIANCE","LISTAGG","RANK","DENSE_RANK",
            "ROW_NUMBER","NTILE","LEAD","LAG","FIRST_VALUE","LAST_VALUE",
            "GREATEST","LEAST","RAISE_APPLICATION_ERROR","CAST","CONVERT",
            "SYS_GUID","USERENV","SYS_CONTEXT","UID","USER","SQLCODE","SQLERRM",
            "HEXTORAW","RAWTOHEX","VSIZE","DUMP","ASCII","CHR",
            "REGEXP_LIKE","REGEXP_SUBSTR","REGEXP_REPLACE","REGEXP_INSTR","STANDARD"
        };

    static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase) {
            "BEGIN","END","IF","THEN","ELSIF","ELSE","LOOP","FOR","WHILE","EXIT",
            "WHEN","RETURN","NULL","TRUE","FALSE","INTO","FROM","WHERE","SELECT",
            "INSERT","UPDATE","DELETE","MERGE","COMMIT","ROLLBACK","SAVEPOINT",
            "OPEN","CLOSE","FETCH","BULK","COLLECT","FORALL","RAISE","PRAGMA",
            "EXECUTE","IMMEDIATE","USING","GOTO","CASE","LOCK","IN","OUT","NOCOPY",
            "IS","AS","DECLARE","PACKAGE","BODY","PROCEDURE","FUNCTION","CURSOR",
            "TYPE","RECORD","TABLE","INDEX","CONSTANT","EXCEPTION","TRIGGER",
            "CREATE","OR","REPLACE","AND","NOT","LIKE","BETWEEN","EXISTS","UNION",
            "INTERSECT","MINUS","GROUP","ORDER","BY","HAVING","JOIN","LEFT","RIGHT",
            "INNER","OUTER","FULL","CROSS","ON","CONNECT","START","WITH","LEVEL",
            "PRIOR","ROWNUM","ROWID","OVER","PARTITION","DISTINCT","ALL","ANY",
            "SET","VALUES","NEW","OLD","ROW","EACH","STATEMENT","OF"
        };

    // ── 파일 파싱 진입점 ────────────────────────────────────
    public PlSqlFile Parse(string filePath, string content) {
      var f = new PlSqlFile { FilePath = filePath, Content = content };
      try {
        var lines = SplitLines(content);
        ParseTopLevel(content, lines, f);
        ResolveRefs(f);
        MarkDeadCode(f);
      }
      catch (Exception ex) { f.Errors.Add("Parser: " + ex.Message); }
      return f;
    }

    // ── 최상위 파싱 ─────────────────────────────────────────
    void ParseTopLevel(string src, string[] lines, PlSqlFile f) {
      var pkgRx = new Regex(
          @"CREATE\s+(?:OR\s+REPLACE\s+)?PACKAGE\s+(BODY\s+)?(\w+)",
          RegexOptions.IgnoreCase);

      var pkgM = pkgRx.Match(src);
      if (pkgM.Success) {
        bool isBody = pkgM.Groups[1].Value.Trim().ToUpper() == "BODY";
        var pkg = new PlSqlObj {
          Name = pkgM.Groups[2].Value,
          Kind = isBody ? ObjType.PackageBody : ObjType.Package,
          StartLine = LineOf(src, pkgM.Index),
          FilePath = f.FilePath
        };
        pkg.EndLine = FindBlockEnd(lines, pkg.StartLine - 1);
        pkg.Source = JoinLines(lines, pkg.StartLine - 1, pkg.EndLine - 1);
        pkg.Metrics = CalcMetrics(lines, pkg.StartLine - 1, pkg.EndLine - 1);

        ParseChildren(src, lines, pkg, f);
        f.Objects.Add(pkg);
      }
      else {
        // standalone PROCEDURE / FUNCTION
        var rx = new Regex(
            @"CREATE\s+(?:OR\s+REPLACE\s+)?(PROCEDURE|FUNCTION)\s+(\w+)",
            RegexOptions.IgnoreCase);
        foreach (Match m in rx.Matches(src))
          f.Objects.Add(BuildObj(src, lines, m.Groups[1].Value,
                                 m.Groups[2].Value, m.Index, null, f.FilePath));
      }
    }

    // ── 패키지 내 자식(프로시저/함수) 파싱 ─────────────────
    void ParseChildren(string src, string[] lines, PlSqlObj parent, PlSqlFile f) {
      var rx = new Regex(
          @"(?:^|[\s;])(PROCEDURE|FUNCTION)\s+(\w+)\s*[\(;]",
          RegexOptions.IgnoreCase | RegexOptions.Multiline);

      foreach (Match m in rx.Matches(src)) {
        int lineNo = LineOf(src, m.Index);
        if (lineNo < parent.StartLine || lineNo > parent.EndLine)
          continue;

        var obj = BuildObj(src, lines, m.Groups[1].Value, m.Groups[2].Value,
                           m.Index, parent, f.FilePath);
        parent.Children.Add(obj);
        f.Objects.Add(obj);
      }
    }

    // ── 단일 오브젝트 빌드 ───────────────────────────────────
    PlSqlObj BuildObj(string src, string[] lines, string kind, string name,
                      int idx, PlSqlObj? parent, string filePath) {
      var obj = new PlSqlObj {
        Name = name,
        Kind = kind.ToUpper() == "PROCEDURE" ? ObjType.Procedure : ObjType.Function,
        StartLine = LineOf(src, idx),
        Parent = parent,
        FilePath = filePath
      };
      obj.Params = ParseParams(src, idx, lines);
      obj.ReturnType = obj.Kind == ObjType.Function ? ExtractReturn(src, idx) : null;
      obj.EndLine = FindProcEnd(lines, obj.StartLine - 1);
      if (obj.EndLine <= obj.StartLine)
        obj.EndLine = obj.StartLine + 1;
      obj.Source = JoinLines(lines, obj.StartLine - 1, obj.EndLine - 1);
      obj.Sqls = ExtractSqls(obj.Source, obj.StartLine);
      obj.Calls = ExtractCalls(obj.Source, obj.StartLine, name);
      obj.Vars = ExtractVars(obj.Source, obj.StartLine);
      obj.Metrics = CalcMetrics(lines, obj.StartLine - 1, obj.EndLine - 1);
      obj.Metrics.Params = obj.Params.Count;
      obj.Metrics.SqlCount = obj.Sqls.Count;
      obj.Metrics.CallCount = obj.Calls.Count;
      obj.Metrics.VarCount = obj.Vars.Count;

      // 미사용 변수 마킹
      foreach (var v in obj.Vars) {
        if (v.IsExcept)
          continue;
        int cnt = Regex.Matches(obj.Source ?? "",
            $@"\b{Regex.Escape(v.Name)}\b",
            RegexOptions.IgnoreCase).Count;
        v.IsUsed = cnt > 1;
      }
      return obj;
    }

    // ── 파라미터 파싱 ────────────────────────────────────────
    List<PlSqlParam> ParseParams(string src, int start, string[] lines) {
      var result = new List<PlSqlParam>();
      int p = src.IndexOf('(', start);
      if (p < 0)
        return result;

      int isAs = FindIsAs(src, start);
      if (isAs > 0 && p > isAs)
        return result;

      int end = MatchingParen(src, p);
      if (end < 0)
        return result;

      string paramStr = src.Substring(p + 1, end - p - 1);
      int baseLine = LineOf(src, p);

      foreach (var part in SplitComma(paramStr)) {
        var pm = ParseOneParam(part.Trim(), baseLine);
        if (pm != null)
          result.Add(pm);
      }
      return result;
    }

    PlSqlParam? ParseOneParam(string s, int line) {
      if (string.IsNullOrWhiteSpace(s))
        return null;
      var rx = new Regex(
          @"^(\w+)\s+(IN\s+OUT|IN\s+NOCOPY|OUT\s+NOCOPY|IN|OUT)?\s*(?:NOCOPY\s+)?" +
          @"([^\s:=]+(?:\s*\([^)]*\))?(?:\s*%\w+)?)\s*(?:(?::=|DEFAULT)\s*(.+))?$",
          RegexOptions.IgnoreCase);
      var m = rx.Match(s);
      if (!m.Success) {
        var t = s.Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);
        if (t.Length < 2)
          return null;
        return new PlSqlParam { Name = t[0], Dir = "IN", DataType = t[^1], Line = line };
      }
      return new PlSqlParam {
        Name = m.Groups[1].Value,
        Dir = string.IsNullOrEmpty(m.Groups[2].Value)
                       ? "IN" : m.Groups[2].Value.Trim().ToUpper(),
        DataType = m.Groups[3].Value.Trim(),
        Default = m.Groups[4].Success ? m.Groups[4].Value.Trim() : null,
        Line = line
      };
    }

    // ── SQL 추출 ─────────────────────────────────────────────
    List<SqlInfo> ExtractSqls(string? src, int baseL) {
      var list = new List<SqlInfo>();
      if (string.IsNullOrEmpty(src))
        return list;
      var clean = Strip(src);

      void Add(string kind, MatchCollection mc) {
        foreach (Match m in mc) {
          int lo = LineOf(clean, m.Index);
          var raw = ExtractStmt(src, m.Index);
          var qi = new SqlInfo { Kind = kind, Raw = raw, StartLine = baseL + lo - 1 };
          if (kind == "SELECT") {
            qi.Columns = ExtractCols(raw);
            qi.Tables = ExtractTables(raw);
            qi.Where = ExtractWhere(raw);
            qi.FromSubqueries = ExtractFromSubs(raw);
            qi.WhereSubqueries = ExtractWhereSubs(raw);
          }
          else if (kind == "UPDATE")
            qi.Tables = ExtractUpdateTable(raw);
          else if (kind == "INSERT")
            qi.Tables = ExtractInsertTable(raw);
          else if (kind == "DELETE")
            qi.Tables = ExtractDeleteTable(raw);
          qi.EndLine = qi.StartLine + raw.Split('\n').Length - 1;
          list.Add(qi);
        }
      }

      Add("SELECT", Regex.Matches(clean, @"\bSELECT\b", RegexOptions.IgnoreCase));
      Add("INSERT", Regex.Matches(clean, @"\bINSERT\s+INTO\b", RegexOptions.IgnoreCase));
      Add("UPDATE", Regex.Matches(clean, @"\bUPDATE\s+\w", RegexOptions.IgnoreCase));
      Add("DELETE", Regex.Matches(clean, @"\bDELETE\b", RegexOptions.IgnoreCase));
      Add("MERGE", Regex.Matches(clean, @"\bMERGE\s+INTO\b", RegexOptions.IgnoreCase));

      return list.OrderBy(q => q.StartLine).ToList();
    }

    List<SqlColumn> ExtractCols(string sql) {
      var cols = new List<SqlColumn>();
      var m = Regex.Match(sql,
          @"\bSELECT\s+(.*?)\s+\bFROM\b",
          RegexOptions.IgnoreCase | RegexOptions.Singleline);
      if (!m.Success)
        return cols;

      var colTxt = m.Groups[1].Value.Trim();
      if (colTxt == "*") {
        cols.Add(new SqlColumn { Expr = "*" });
        return cols;
      }

      foreach (var part in SplitComma(colTxt)) {
        var t = part.Trim();
        if (string.IsNullOrEmpty(t))
          continue;
        var col = new SqlColumn();
        bool hasSub = Regex.IsMatch(t, @"\bSELECT\b", RegexOptions.IgnoreCase);
        if (hasSub || t.StartsWith("(")) {
          col.IsSubquery = true;
          col.Expr = t.Length > 80 ? t[..80] + "..." : t;
          int lp = t.LastIndexOf(')');
          if (lp >= 0 && lp < t.Length - 1)
            col.Alias = t[(lp + 1)..].Trim().Trim('"');
        }
        else {
          var am = Regex.Match(t,
              @"^(.*?)\s+(?:AS\s+)?(\w+)\s*$", RegexOptions.IgnoreCase);
          if (am.Success && !Keywords.Contains(am.Groups[2].Value)) {
            col.Expr = am.Groups[1].Value.Trim();
            col.Alias = am.Groups[2].Value;
          }
          else
            col.Expr = t;
        }
        cols.Add(col);
      }
      return cols;
    }

    List<string> ExtractTables(string sql) {
      var tables = new List<string>();
      var m = Regex.Match(sql,
          @"\bFROM\b(.*?)(?:\bWHERE\b|\bGROUP\b|\bORDER\b|\bHAVING\b|\bCONNECT\b|;|$)",
          RegexOptions.IgnoreCase | RegexOptions.Singleline);
      if (!m.Success)
        return tables;

      foreach (var part in Regex.Split(m.Groups[1].Value,
          @",|\b(?:INNER|LEFT|RIGHT|FULL|CROSS|OUTER)?\s*JOIN\b",
          RegexOptions.IgnoreCase)) {
        var t = part.Trim();
        if (string.IsNullOrEmpty(t) || t.StartsWith("("))
          continue;
        var tm = Regex.Match(t, @"^([\w.$]+)(?:\s+(?:AS\s+)?(\w+))?");
        if (tm.Success)
          tables.Add(tm.Groups[2].Success
              ? $"{tm.Groups[1].Value}({tm.Groups[2].Value})"
              : tm.Groups[1].Value);
      }
      return tables;
    }

    List<string> ExtractUpdateTable(string sql) {
      var m = Regex.Match(sql, @"\bUPDATE\s+([\w.$]+)", RegexOptions.IgnoreCase);
      return m.Success ? new List<string> { m.Groups[1].Value } : new();
    }
    List<string> ExtractInsertTable(string sql) {
      var m = Regex.Match(sql, @"\bINSERT\s+INTO\s+([\w.$]+)", RegexOptions.IgnoreCase);
      return m.Success ? new List<string> { m.Groups[1].Value } : new();
    }
    List<string> ExtractDeleteTable(string sql) {
      var m = Regex.Match(sql,
          @"\bDELETE\s+(?:FROM\s+)?([\w.$]+)", RegexOptions.IgnoreCase);
      return m.Success ? new List<string> { m.Groups[1].Value } : new();
    }
    string? ExtractWhere(string sql) {
      var m = Regex.Match(sql,
          @"\bWHERE\b(.*?)(?:\bGROUP\b|\bORDER\b|\bHAVING\b|\bSTART\b|;|$)",
          RegexOptions.IgnoreCase | RegexOptions.Singleline);
      return m.Success ? m.Groups[1].Value.Trim() : null;
    }
    List<string> ExtractFromSubs(string sql) {
      var list = new List<string>();
      var m = Regex.Match(sql,
          @"\bFROM\b(.*?)(?:\bWHERE\b|;|$)",
          RegexOptions.IgnoreCase | RegexOptions.Singleline);
      if (!m.Success)
        return list;
      foreach (Match sm in Regex.Matches(m.Groups[1].Value,
          @"\(\s*(SELECT\b[^)]{0,300})", RegexOptions.IgnoreCase))
        list.Add(sm.Groups[1].Value.Trim());
      return list;
    }
    List<string> ExtractWhereSubs(string sql) {
      var list = new List<string>();
      var wh = ExtractWhere(sql);
      if (wh == null)
        return list;
      foreach (Match sm in Regex.Matches(wh,
          @"\(\s*(SELECT\b[^)]{0,300})", RegexOptions.IgnoreCase))
        list.Add(sm.Groups[1].Value.Trim());
      return list;
    }

    // ── 호출 추출 ────────────────────────────────────────────
    List<CallRef> ExtractCalls(string? src, int baseL, string self) {
      var list = new List<CallRef>();
      if (string.IsNullOrEmpty(src))
        return list;
      var clean = Strip(src);

      var rx = new Regex(
          @"(?<![.'\w])(\w+)\.(\w+)\s*\(|(?<![.':\w])(\w+)\s*\(",
          RegexOptions.Compiled);

      foreach (Match m in rx.Matches(clean)) {
        string name;
        string? pkg = null;
        if (m.Groups[1].Success) {
          pkg = m.Groups[1].Value;
          name = m.Groups[2].Value;
        }
        else
          name = m.Groups[3].Value;

        if (Keywords.Contains(name) || Builtins.Contains(name))
          continue;
        if (string.Equals(name, self, StringComparison.OrdinalIgnoreCase))
          continue;
        if (name.Length <= 1)
          continue;

        int ln = baseL + LineOf(clean, m.Index) - 1;
        if (!list.Any(c => c.Name == name && c.PkgName == pkg && c.Line == ln))
          list.Add(new CallRef { PkgName = pkg, Name = name, Line = ln });
      }
      return list.OrderBy(c => c.Line).ToList();
    }

    // ── 변수 추출 ────────────────────────────────────────────
    List<PlSqlVar> ExtractVars(string? src, int baseL) {
      var list = new List<PlSqlVar>();
      if (string.IsNullOrEmpty(src))
        return list;
      bool inDecl = false;
      int depth = 0;
      var lines = SplitLines(src);

      for (int i = 0; i < lines.Length; i++) {
        var raw = lines[i];
        var upper = StripInline(raw).Trim().ToUpper();

        if (Regex.IsMatch(upper, @"^\s*(DECLARE|IS|AS)\b"))
          inDecl = true;
        if (Regex.IsMatch(upper, @"^\s*BEGIN\b")) {
          depth++;
          if (depth == 1)
            inDecl = false;
        }
        if (Regex.IsMatch(upper, @"^\s*END\b"))
          depth = Math.Max(0, depth - 1);
        if (!inDecl)
          continue;

        // EXCEPTION 선언
        var ex = Regex.Match(raw,
            @"^\s*(\w+)\s+EXCEPTION\s*;", RegexOptions.IgnoreCase);
        if (ex.Success) {
          list.Add(new PlSqlVar {
            Name = ex.Groups[1].Value,
            IsExcept = true,
            DataType = "EXCEPTION",
            Line = baseL + i
          });
          continue;
        }
        // 변수 / 상수
        var vm = Regex.Match(raw,
            @"^\s*(\w+)\s+(CONSTANT\s+)?(\w+(?:\s*\([^)]*\))?(?:\s*%\w+)?)\s*(?::=\s*(.+?))?;",
            RegexOptions.IgnoreCase);
        if (vm.Success && !Keywords.Contains(vm.Groups[1].Value.ToUpper()))
          list.Add(new PlSqlVar {
            Name = vm.Groups[1].Value,
            IsConst = vm.Groups[2].Success,
            DataType = vm.Groups[3].Value.Trim(),
            Default = vm.Groups[4].Success ? vm.Groups[4].Value.Trim() : null,
            Line = baseL + i
          });
      }
      return list;
    }

    // ── 메트릭스 계산 ────────────────────────────────────────
    Metrics CalcMetrics(string[] all, int s, int e) {
      var m = new Metrics();
      if (s < 0 || e >= all.Length || s > e)
        return m;
      m.Total = e - s + 1;
      bool inBlock = false;
      int depth = 0;

      for (int i = s; i <= e; i++) {
        var t = all[i].Trim();
        if (string.IsNullOrWhiteSpace(t)) {
          m.Blank++;
          continue;
        }
        if (inBlock) {
          m.Comments++;
          if (t.Contains("*/"))
            inBlock = false;
          continue;
        }
        if (t.StartsWith("/*")) {
          m.Comments++;
          if (!t.Contains("*/"))
            inBlock = true;
          continue;
        }
        if (t.StartsWith("--")) {
          m.Comments++;
          continue;
        }

        m.Code++;
        var u = t.ToUpper();
        if (Regex.IsMatch(u, @"\bIF\b"))
          m.Complexity++;
        if (Regex.IsMatch(u, @"\bELSIF\b"))
          m.Complexity++;
        if (Regex.IsMatch(u, @"\bWHEN\b"))
          m.Complexity++;
        if (Regex.IsMatch(u, @"\bLOOP\b"))
          m.Complexity++;
        if (Regex.IsMatch(u, @"\bWHILE\b"))
          m.Complexity++;
        if (Regex.IsMatch(u, @"\bEXCEPTION\b"))
          m.Complexity++;

        if (Regex.IsMatch(u, @"\b(BEGIN|IF|LOOP|CASE)\b")) {
          depth++;
          m.MaxNesting = Math.Max(m.MaxNesting, depth);
        }
        if (Regex.IsMatch(u, @"\bEND\b"))
          depth = Math.Max(0, depth - 1);
      }
      return m;
    }

    // ── 교차 참조 해결 ───────────────────────────────────────
    void ResolveRefs(PlSqlFile f) {
      var lkp = new Dictionary<string, PlSqlObj>(StringComparer.OrdinalIgnoreCase);
      foreach (var o in f.Objects)
        if (!lkp.ContainsKey(o.Name))
          lkp[o.Name] = o;
      foreach (var o in f.Objects)
        foreach (var c in o.Calls)
          if (lkp.TryGetValue(c.Name, out var t))
            c.Target = t;
    }

    void MarkDeadCode(PlSqlFile f) {
      var called = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach (var o in f.Objects)
        foreach (var c in o.Calls)
          called.Add(c.Name);
      foreach (var o in f.Objects)
        if (o.Kind == ObjType.Procedure || o.Kind == ObjType.Function)
          o.IsNeverCalled = !called.Contains(o.Name);
    }

    // ── 공통 유틸 ────────────────────────────────────────────
    string Strip(string src) {
      var sb = new StringBuilder(src.Length);
      bool inStr = false, inLc = false, inBc = false;
      for (int i = 0; i < src.Length; i++) {
        char c = src[i], nx = i + 1 < src.Length ? src[i + 1] : '\0';
        if (inStr) {
          sb.Append(c == '\n' ? '\n' : ' ');
          if (c == '\'' && nx != '\'')
            inStr = false;
          else if (c == '\'' && nx == '\'') {
            sb.Append(' ');
            i++;
          }
          continue;
        }
        if (inLc) {
          if (c == '\n') {
            inLc = false;
            sb.Append('\n');
          }
          else
            sb.Append(' ');
          continue;
        }
        if (inBc) {
          if (c == '*' && nx == '/') {
            inBc = false;
            i++;
            sb.Append("  ");
          }
          else
            sb.Append(c == '\n' ? '\n' : ' ');
          continue;
        }
        if (c == '\'') {
          inStr = true;
          sb.Append(' ');
        }
        else if (c == '-' && nx == '-') {
          inLc = true;
          i++;
          sb.Append("  ");
        }
        else if (c == '/' && nx == '*') {
          inBc = true;
          i++;
          sb.Append("  ");
        }
        else
          sb.Append(c);
      }
      return sb.ToString();
    }

    // 줄 번호(0-based) → src 내 문자 오프셋
    int LineOffset(string src, int lineIndex) {
      int pos = 0, cnt = 0;
      while (pos < src.Length && cnt < lineIndex) {
        if (src[pos] == '\n')
          cnt++;
        pos++;
      }
      return pos;
    }

    int LineOf(string s, int idx) {
      int n = 1;
      for (int i = 0; i < idx && i < s.Length; i++)
      if (s[i] == '\n')
        n++;
      return n;
    }
    string[] SplitLines(string s) => s.Split('\n');
    string JoinLines(string[] ls, int s, int e) {
      if (s < 0)
        s = 0;
      if (e >= ls.Length)
        e = ls.Length - 1;
      if (s > e)
        return "";
      return string.Join("\n", ls, s, e - s + 1);
    }
    int MatchingParen(string s, int open) {
      int d = 0;
      bool inStr = false;
      for (int i = open; i < s.Length; i++) {
        char c = s[i];
        if (c == '\'' && !inStr)
          inStr = true;
        else if (c == '\'' && inStr)
          inStr = false;
        if (!inStr) {
          if (c == '(')
            d++;
          else if (c == ')') {
            d--;
            if (d == 0)
              return i;
          }
        }
      }
      return -1;
    }
    List<string> SplitComma(string s) {
      var r = new List<string>();
      int d = 0, st = 0;
      bool inStr = false;
      for (int i = 0; i < s.Length; i++) {
        char c = s[i];
        if (c == '\'' && !inStr)
          inStr = true;
        else if (c == '\'' && inStr)
          inStr = false;
        if (!inStr) {
          if (c == '(' || c == '[')
            d++;
          else if (c == ')' || c == ']')
            d--;
          else if (c == ',' && d == 0) {
            r.Add(s.Substring(st, i - st));
            st = i + 1;
          }
        }
      }
      if (st < s.Length)
        r.Add(s.Substring(st));
      return r;
    }
    string ExtractStmt(string src, int si) {
      if (si >= src.Length)
        return "";
      var sb = new StringBuilder();
      int d = 0;
      for (int i = si; i < src.Length; i++) {
        char c = src[i];
        sb.Append(c);
        if (c == '(')
          d++;
        else if (c == ')')
          d--;
        else if (c == ';' && d == 0)
          break;
      }
      return sb.ToString();
    }
    int FindIsAs(string src, int start) {
      var m = Regex.Match(
          src.Substring(start, Math.Min(2000, src.Length - start)),
          @"\b(IS|AS)\b", RegexOptions.IgnoreCase);
      return m.Success ? start + m.Index : -1;
    }
    int FindBlockEnd(string[] lines, int start) {
      int depth = 0;
      bool started = false;
      for (int i = start; i < lines.Length; i++) {
        var u = StripInline(lines[i]).Trim().ToUpper();
        int bg = Regex.Matches(u, @"\bBEGIN\b").Count;
        int en = Regex.Matches(u, @"\bEND\b").Count;
        depth += bg;
        if (bg > 0)
          started = true;
        if (started) {
          depth -= en;
          if (depth <= 0)
            return i + 1;
        }
        else if (en > 0)
          return i + 1;
      }
      return lines.Length;
    }
    int FindProcEnd(string[] lines, int start) {
      int depth = 0;
      bool foundBegin = false;
      for (int i = start; i < lines.Length; i++) {
        var u = StripInline(lines[i]).Trim().ToUpper();
        foreach (Match w in Regex.Matches(u, @"\b(BEGIN|END|IF|LOOP|CASE)\b")) {
          if (w.Value == "BEGIN") {
            depth++;
            foundBegin = true;
          }
          else if (w.Value == "END")
            depth--;
        }
        if (foundBegin && depth <= 0)
          return i + 1;
      }
      return lines.Length;
    }
    string StripInline(string line) {
      int ci = line.IndexOf("--");
      if (ci < 0)
        return line;
      bool inS = false;
      for (int i = 0; i < ci; i++)
      if (line[i] == '\'')
        inS = !inS;
      return inS ? line : line[..ci];
    }
    string? ExtractReturn(string src, int start) {
      var seg = src.Substring(start, Math.Min(600, src.Length - start));
      var m = Regex.Match(seg,
          @"\bRETURN\s+(\w+(?:\s*\([^)]*\))?)", RegexOptions.IgnoreCase);
      return m.Success ? m.Groups[1].Value : null;
    }
  }
}