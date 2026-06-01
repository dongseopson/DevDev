using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

using ICSharpCode.AvalonEdit.Document;

using PlSqlAnalyzer.Models;
using PlSqlAnalyzer.Parser;

namespace PlSqlAnalyzer.ViewModels {
  public class RelayCommand : ICommand {
    readonly Action<object?> _exec; readonly Func<object?, bool>? _can;
    public RelayCommand(Action<object?> e, Func<object?, bool>? c = null) {
      _exec = e;
      _can = c;
    }
    public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
    public void Execute(object? p) => _exec(p);
    public event EventHandler? CanExecuteChanged {
      add => CommandManager.RequerySuggested += value;
      remove => CommandManager.RequerySuggested -= value;
    }
  }

  public class MainViewModel : INotifyPropertyChanged {
    readonly PlSqlParser _parser = new();
    readonly PlSqlFormatter _formatter = new();
    public FormatOptions FormOpts { get; } = new();

    // ── Collections ──────────────────────────────────────────
    public ObservableCollection<PlSqlFile> Files { get; } = new();
    public ObservableCollection<PlSqlObj> TreeRoots { get; } = new();
    public ObservableCollection<PlSqlObj> SearchResults { get; } = new();
    public ObservableCollection<CrudRow> CrudMatrix { get; } = new();

    // ── CurrentFile ──────────────────────────────────────────
    PlSqlFile? _curFile;
    public PlSqlFile? CurrentFile {
      get => _curFile;
      set {
        _curFile = value;
        PC();
        UpdateTree();
      }
    }

    // ── SelectedObject → 우측 패널 모두 갱신 ────────────────
    PlSqlObj? _selObj;
    public PlSqlObj? SelectedObject {
      get => _selObj;
      set {
        _selObj = value;
        PC();
        PC(nameof(SelParams));
        PC(nameof(SelSqls));
        PC(nameof(SelCalls));
        PC(nameof(SelVars));
        PC(nameof(SelMetrics));
        PC(nameof(SelSource));
      }
    }

    // ── Analysis 프로퍼티 (우측 패널 바인딩) ────────────────
    public IEnumerable<PlSqlParam>? SelParams => _selObj?.Params;
    public IEnumerable<SqlInfo>? SelSqls => _selObj?.Sqls;
    public IEnumerable<CallRef>? SelCalls => _selObj?.Calls;
    public IEnumerable<PlSqlVar>? SelVars => _selObj?.Vars;
    public Metrics? SelMetrics => _selObj?.Metrics;
    public string? SelSource => _selObj?.Source;

    // ── Status ───────────────────────────────────────────────
    string _status = "Ready — PL/SQL 파일을 드래그하거나 파일열기로 로드하세요.";
    public string Status {
      get => _status;
      set {
        _status = value;
        PC();
      }
    }

    // ── Search ───────────────────────────────────────────────
    string _search = "";
    public string SearchText {
      get => _search;
      set {
        _search = value;
        PC();
        DoSearch();
      }
    }

    // ── 이벤트 (MainWindow가 구독) ───────────────────────────
    public event Action<PlSqlFile, int>? NavigateRequested;
    public event Action<string>? ShowMessageRequested;

    // ── Commands ─────────────────────────────────────────────
    public ICommand FormatCmd {
      get;
    }
    public ICommand NoCommentCmd {
      get;
    }
    public ICommand ExportCmd {
      get;
    }
    public ICommand CopySourceCmd {
      get;
    }
    public ICommand CrudCmd {
      get;
    }

    public MainViewModel() {
      FormatCmd = new RelayCommand(_ => FormatCurrent());
      NoCommentCmd = new RelayCommand(_ => RemoveComments());
      ExportCmd = new RelayCommand(_ => ExportHtml());
      CopySourceCmd = new RelayCommand(_ => CopySource());
      CrudCmd = new RelayCommand(_ => BuildCrud());
    }

    // ── 파일 로드 ────────────────────────────────────────────
    public void LoadFile(string path) {
      try {
        // 이미 열려 있으면 전환만
        if (Files.Any(f => f.FilePath == path)) {
          CurrentFile = Files.First(f => f.FilePath == path);
          NavigateRequested?.Invoke(CurrentFile, 1);
          return;
        }
        var content = File.ReadAllText(path);
        LoadContent(path, content);
      }
      catch (Exception ex) { Status = $"파일 로드 오류: {ex.Message}"; }
    }

    public void LoadContent(string fakePath, string content) {
      var pf = _parser.Parse(fakePath, content);
      // Doc은 OnNavigate에서 Editor.Document와 통일되므로 여기선 미리 생성만
      pf.Doc = new ICSharpCode.AvalonEdit.Document.TextDocument(content);
      Files.Add(pf);
      CurrentFile = pf;

      int procs = pf.Objects.Count(o =>
          o.Kind == ObjType.Procedure || o.Kind == ObjType.Function);
      Status = $"로드: {pf.FileName}  |  오브젝트 {pf.Objects.Count}개  " +
               $"|  프로시저/함수 {procs}개" +
               (pf.Errors.Count > 0 ? $"  |  ⚠ 경고 {pf.Errors.Count}건" : "");

      // 에디터로 이동
      NavigateRequested?.Invoke(pf, 1);
    }

    // ── 트리 갱신 ────────────────────────────────────────────
    void UpdateTree() {
      TreeRoots.Clear();
      if (_curFile == null)
        return;
      foreach (var o in _curFile.Objects.Where(o => o.Parent == null)) {
        o.IsExpanded = true;
        TreeRoots.Add(o);
      }
    }

    // ── 오브젝트 선택 ────────────────────────────────────────
    public void SelectObject(PlSqlObj? obj) {
      SelectedObject = obj;
    }

    // ── 호출 탐색 ────────────────────────────────────────────
    public void NavigateToCall(CallRef cr) {
      var target = cr.Target
          ?? Files.SelectMany(f => f.Objects)
                   .FirstOrDefault(o => string.Equals(o.Name, cr.Name,
                       StringComparison.OrdinalIgnoreCase));

      if (target != null) {
        SelectedObject = target;
        var file = Files.FirstOrDefault(f => f.FilePath == target.FilePath);
        if (file != null)
          NavigateRequested?.Invoke(file, target.StartLine);
        return;
      }
      ShowMessageRequested?.Invoke(
          $"'{cr.FullName}' 을(를) 로드된 파일에서 찾지 못했습니다.\n" +
          $"해당 패키지 파일을 추가로 열면 탐색이 가능합니다.");
    }

    // ── 검색 ─────────────────────────────────────────────────
    void DoSearch() {
      SearchResults.Clear();
      if (string.IsNullOrWhiteSpace(_search) || _search.Length < 2)
        return;
      var q = _search.ToUpper();
      foreach (var f in Files)
        foreach (var o in f.Objects)
          if (o.Name.ToUpper().Contains(q))
            SearchResults.Add(o);
    }

    // ── 포맷 ─────────────────────────────────────────────────
    void FormatCurrent() {
      if (_curFile?.Doc == null)
        return;
      var formatted = _formatter.Format(_curFile.Doc.Text, FormOpts);
      _curFile.Doc.Text = formatted;
      Status = "포맷 완료.";
    }

    // ── 주석 제거 ────────────────────────────────────────────
    void RemoveComments() {
      if (_curFile?.Doc == null)
        return;
      _curFile.Doc.Text = _formatter.RemoveComments(_curFile.Doc.Text);
      Status = "주석 제거 완료.";
    }

    // ── 소스 복사 ────────────────────────────────────────────
    void CopySource() {
      if (_selObj?.Source != null)
        System.Windows.Clipboard.SetText(_selObj.Source);
    }

    // ── CRUD 매트릭스 ────────────────────────────────────────
    void BuildCrud() {
      CrudMatrix.Clear();
      var allObjs = Files.SelectMany(f => f.Objects)
                         .Where(o => o.Kind == ObjType.Procedure
                                  || o.Kind == ObjType.Function);
      foreach (var obj in allObjs) {
        var row = new CrudRow {
          Name = obj.Name,
          Package = obj.Parent?.Name ?? ""
        };
        foreach (var sq in obj.Sqls)
          foreach (var tbl in sq.Tables)
            switch (sq.Kind) {
            case "SELECT":
              row.Selects.Add(tbl);
              break;
            case "INSERT":
              row.Inserts.Add(tbl);
              break;
            case "UPDATE":
              row.Updates.Add(tbl);
              break;
            case "DELETE":
              row.Deletes.Add(tbl);
              break;
            case "MERGE":
              row.Merges.Add(tbl);
              break;
            }
        if (row.HasAny)
          CrudMatrix.Add(row);
      }
      Status = $"CRUD 매트릭스 완료: {CrudMatrix.Count}개 오브젝트";
    }

    // ── HTML 리포트 ──────────────────────────────────────────
    void ExportHtml() {
      if (_curFile == null)
        return;
      var dlg = new Microsoft.Win32.SaveFileDialog {
        Filter = "HTML|*.html",
        FileName = _curFile.FileName + "_analysis.html"
      };
      if (dlg.ShowDialog() != true)
        return;

      var sb = new StringBuilder();
      sb.Append(@"<!DOCTYPE html><html><head><meta charset='utf-8'>
<title>PL/SQL Analysis</title>
<style>
body{font-family:Consolas,monospace;background:#1e1e1e;color:#d4d4d4;margin:20px}
h1{color:#569cd6}h2{color:#4ec9b0;border-bottom:1px solid #3c3c3c;padding-bottom:4px}
h3{color:#c586c0}table{border-collapse:collapse;width:100%;margin-bottom:16px}
th{background:#252526;color:#9cdcfe;padding:6px 10px;text-align:left}
td{padding:5px 10px;border-bottom:1px solid #2d2d2d}
.warn{color:#ff8c00}.err{color:#f44747}.ok{color:#4ec9b0}
pre{background:#252526;padding:12px;border-radius:4px;overflow-x:auto;font-size:12px}
</style></head><body>");

      sb.Append($"<h1>PL/SQL Analysis — {_curFile.FileName}</h1>");
      sb.Append($"<p>생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

      foreach (var obj in _curFile.Objects.Where(
          o => o.Parent == null || o.Kind == ObjType.Procedure || o.Kind == ObjType.Function)) {
        sb.Append($"<h2>{obj.Icon} {obj.DisplayName} " +
                  $"<small style='color:#808080'>{obj.LineInfo}</small>");
        if (obj.IsNeverCalled)
          sb.Append(" <span class='warn'>⚠ Dead Code?</span>");
        sb.Append("</h2>");

        if (obj.Params.Count > 0) {
          sb.Append("<h3>Parameters</h3><table>" +
                    "<tr><th>Name</th><th>Dir</th><th>Type</th><th>Default</th></tr>");
          foreach (var p in obj.Params)
            sb.Append($"<tr><td>{p.Name}</td><td>{p.Dir}</td>" +
                      $"<td>{p.DataType}</td><td>{p.Default ?? ""}</td></tr>");
          sb.Append("</table>");
        }
        if (obj.Sqls.Count > 0) {
          sb.Append("<h3>SQL Queries</h3><table>" +
                    "<tr><th>#</th><th>Kind</th><th>Tables</th><th>Cols</th><th>Line</th></tr>");
          int qi = 0;
          foreach (var q in obj.Sqls)
            sb.Append($"<tr><td>{++qi}</td><td>{q.Kind}</td>" +
                      $"<td>{string.Join(", ", q.Tables)}</td>" +
                      $"<td>{q.Columns.Count}</td><td>{q.StartLine}</td></tr>");
          sb.Append("</table>");
        }
        var m = obj.Metrics;
        sb.Append($"<p><b>Metrics:</b> {m.Total}줄 | 복잡도 " +
                  $"<span class='{(m.Complexity > 10 ? "err" : m.Complexity > 5 ? "warn" : "ok")}'>" +
                  $"{m.Complexity}</span> | 최대중첩 {m.MaxNesting}</p>");
      }
      sb.Append("</body></html>");
      File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
      Status = $"HTML 저장: {dlg.FileName}";
      try {
        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(dlg.FileName) {
              UseShellExecute = true
            });
      }
      catch { }
    }

    // ── INotifyPropertyChanged ───────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    void PC([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
  }
}
