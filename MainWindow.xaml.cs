using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

using Microsoft.Win32;

using PlSqlAnalyzer.Models;
using PlSqlAnalyzer.ViewModels;
namespace PlSqlAnalyzer {
  public partial class MainWindow : Window {
    readonly MainViewModel _vm = new();
    FoldingManager? _foldMgr;
    PlSqlFolding? _foldStrat;

    public MainWindow() {
      InitializeComponent();
      DataContext = _vm;
      _vm.NavigateRequested += OnNavigate;
      _vm.ShowMessageRequested += msg =>
          MessageBox.Show(msg, "탐색", MessageBoxButton.OK, MessageBoxImage.Information);
      SetupEditor();
      UpdateFileCount();
    }

    // ── 에디터 초기 설정 ─────────────────────────────────────
    void SetupEditor() {
      // ★ 임베디드 리소스 대신 인라인 문자열로 로드 (로딩 실패 방지)
      try {
        using var sr = new System.IO.StringReader(PlSqlXshd);
        using var xr = new System.Xml.XmlTextReader(sr);
        Editor.SyntaxHighlighting = HighlightingLoader.Load(
            xr, HighlightingManager.Instance);
      }
      catch (Exception ex) {
        _vm.Status = $"하이라이팅 로드 실패: {ex.Message}";
      }

      Editor.Options.ShowTabs = false;
      Editor.Options.ShowSpaces = false;
      Editor.Options.EnableHyperlinks = false;
      Editor.Options.ConvertTabsToSpaces = true;
      Editor.Options.IndentationSize = 4;
      Editor.Options.HighlightCurrentLine = true;

      _foldMgr = FoldingManager.Install(Editor.TextArea);
      _foldStrat = new PlSqlFolding();

      Editor.TextChanged += (s, e) => {
        if (_foldMgr != null)
          _foldStrat?.UpdateFoldings(_foldMgr, Editor.Document);
      };

      Editor.TextArea.Caret.PositionChanged += (s, e) => {
        var c = Editor.TextArea.Caret;
        TbCaret.Text = $"L{c.Line}  C{c.Column}";
      };

      // ★ Ctrl+V 는 AvalonEdit이 KeyDown을 소비하므로 PreviewKeyDown 사용
      Editor.TextArea.PreviewKeyDown += (s, e) => {
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) {
          // 붙여넣기 완료 후 분석 (Background 우선순위로 대기)
          Dispatcher.BeginInvoke(
              System.Windows.Threading.DispatcherPriority.Background,
              () => AnalyzeEditorContent());
        }
      };

      Editor.ContextMenu = BuildContextMenu();
    }

    // ── PL/SQL 하이라이팅 정의 (인라인) ─────────────────────
    static readonly string PlSqlXshd = @"<?xml version=""1.0""?>
<SyntaxDefinition name=""PLSQL"" extensions="".sql;.pls;.pkb;.pks;.pck""
    xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
  <Color name=""Comment""  foreground=""#608B4E"" />
  <Color name=""String""   foreground=""#CE9178"" />
  <Color name=""Keyword""  foreground=""#569CD6"" fontWeight=""bold""/>
  <Color name=""DML""      foreground=""#C586C0"" fontWeight=""bold""/>
  <Color name=""DataType"" foreground=""#4EC9B0"" />
  <Color name=""Number""   foreground=""#B5CEA8"" />
  <Color name=""BuiltIn""  foreground=""#DCDCAA"" />
  <RuleSet ignoreCase=""true"">
    <Span color=""Comment"" begin=""--"" />
    <Span color=""Comment"" multiline=""true""><Begin>/\*</Begin><End>\*/</End></Span>
    <Span color=""String""><Begin>'</Begin><End>'</End></Span>
    <Keywords color=""Keyword"">
      <Word>PACKAGE</Word><Word>BODY</Word><Word>PROCEDURE</Word><Word>FUNCTION</Word>
      <Word>BEGIN</Word><Word>END</Word><Word>DECLARE</Word><Word>IS</Word><Word>AS</Word>
      <Word>RETURN</Word><Word>IF</Word><Word>THEN</Word><Word>ELSIF</Word><Word>ELSE</Word>
      <Word>LOOP</Word><Word>FOR</Word><Word>WHILE</Word><Word>EXIT</Word><Word>WHEN</Word>
      <Word>CURSOR</Word><Word>OPEN</Word><Word>CLOSE</Word><Word>FETCH</Word><Word>INTO</Word>
      <Word>BULK</Word><Word>COLLECT</Word><Word>FORALL</Word><Word>EXCEPTION</Word>
      <Word>RAISE</Word><Word>PRAGMA</Word><Word>TYPE</Word><Word>RECORD</Word>
      <Word>CONSTANT</Word><Word>NULL</Word><Word>TRUE</Word><Word>FALSE</Word>
      <Word>IN</Word><Word>OUT</Word><Word>NOCOPY</Word><Word>DEFAULT</Word>
      <Word>TRIGGER</Word><Word>BEFORE</Word><Word>AFTER</Word><Word>CREATE</Word>
      <Word>OR</Word><Word>REPLACE</Word><Word>COMMIT</Word><Word>ROLLBACK</Word>
      <Word>EXECUTE</Word><Word>IMMEDIATE</Word><Word>USING</Word><Word>CASE</Word>
      <Word>GOTO</Word><Word>LOCK</Word><Word>NOWAIT</Word><Word>SAVEPOINT</Word>
    </Keywords>
    <Keywords color=""DML"">
      <Word>SELECT</Word><Word>FROM</Word><Word>WHERE</Word><Word>INSERT</Word>
      <Word>UPDATE</Word><Word>DELETE</Word><Word>MERGE</Word><Word>SET</Word>
      <Word>VALUES</Word><Word>AND</Word><Word>NOT</Word><Word>LIKE</Word>
      <Word>BETWEEN</Word><Word>EXISTS</Word><Word>ANY</Word><Word>ALL</Word>
      <Word>DISTINCT</Word><Word>UNION</Word><Word>INTERSECT</Word><Word>MINUS</Word>
      <Word>GROUP</Word><Word>BY</Word><Word>HAVING</Word><Word>ORDER</Word>
      <Word>ASC</Word><Word>DESC</Word><Word>JOIN</Word><Word>LEFT</Word>
      <Word>RIGHT</Word><Word>INNER</Word><Word>OUTER</Word><Word>FULL</Word>
      <Word>CROSS</Word><Word>ON</Word><Word>CONNECT</Word><Word>START</Word>
      <Word>WITH</Word><Word>LEVEL</Word><Word>PRIOR</Word><Word>ROWNUM</Word>
      <Word>ROWID</Word><Word>OVER</Word><Word>PARTITION</Word>
    </Keywords>
    <Keywords color=""DataType"">
      <Word>VARCHAR2</Word><Word>VARCHAR</Word><Word>CHAR</Word><Word>NCHAR</Word>
      <Word>NVARCHAR2</Word><Word>NUMBER</Word><Word>INTEGER</Word><Word>INT</Word>
      <Word>FLOAT</Word><Word>REAL</Word><Word>DATE</Word><Word>TIMESTAMP</Word>
      <Word>INTERVAL</Word><Word>BOOLEAN</Word><Word>CLOB</Word><Word>BLOB</Word>
      <Word>NCLOB</Word><Word>RAW</Word><Word>LONG</Word><Word>PLS_INTEGER</Word>
      <Word>BINARY_INTEGER</Word><Word>BINARY_FLOAT</Word><Word>BINARY_DOUBLE</Word>
      <Word>SIMPLE_INTEGER</Word><Word>XMLTYPE</Word><Word>UROWID</Word>
    </Keywords>
    <Keywords color=""BuiltIn"">
      <Word>NVL</Word><Word>NVL2</Word><Word>DECODE</Word><Word>COALESCE</Word>
      <Word>NULLIF</Word><Word>SUBSTR</Word><Word>INSTR</Word><Word>LENGTH</Word>
      <Word>UPPER</Word><Word>LOWER</Word><Word>INITCAP</Word><Word>TRIM</Word>
      <Word>LTRIM</Word><Word>RTRIM</Word><Word>LPAD</Word><Word>RPAD</Word>
      <Word>REPLACE</Word><Word>TO_CHAR</Word><Word>TO_DATE</Word><Word>TO_NUMBER</Word>
      <Word>TO_TIMESTAMP</Word><Word>SYSDATE</Word><Word>SYSTIMESTAMP</Word>
      <Word>TRUNC</Word><Word>ROUND</Word><Word>FLOOR</Word><Word>CEIL</Word>
      <Word>ABS</Word><Word>MOD</Word><Word>POWER</Word><Word>SQRT</Word>
      <Word>COUNT</Word><Word>SUM</Word><Word>AVG</Word><Word>MAX</Word><Word>MIN</Word>
      <Word>LISTAGG</Word><Word>RANK</Word><Word>DENSE_RANK</Word><Word>ROW_NUMBER</Word>
      <Word>LEAD</Word><Word>LAG</Word><Word>GREATEST</Word><Word>LEAST</Word>
      <Word>RAISE_APPLICATION_ERROR</Word><Word>DBMS_OUTPUT</Word>
      <Word>SQLCODE</Word><Word>SQLERRM</Word><Word>SYS_GUID</Word>
    </Keywords>
    <Rule color=""Number"">\b[0-9]+(\.[0-9]+)?\b</Rule>
  </RuleSet>
</SyntaxDefinition>";

    // ── 컨텍스트 메뉴 ────────────────────────────────────────
    System.Windows.Controls.ContextMenu BuildContextMenu() {
      var cm = new System.Windows.Controls.ContextMenu();
      cm.Background = System.Windows.Media.Brushes.DimGray;

      MenuItem(cm, "찾기 (Ctrl+F)", () => ShowFindBar());
      MenuItem(cm, "선택 복사", () => Editor.Copy());
      MenuItem(cm, "클립보드에서 PL/SQL 붙여넣기 (Ctrl+Shift+V)",
                                                        () => PasteFromClipboard());
      cm.Items.Add(new System.Windows.Controls.Separator());
      MenuItem(cm, "✔ 에디터 내용 분석하기", () => AnalyzeEditorContent());
      MenuItem(cm, "선택 텍스트로 오브젝트 탐색", () => {
        var sel = Editor.SelectedText.Trim();
        if (!string.IsNullOrEmpty(sel)) {
          _vm.SearchText = sel;
          TbSearch.Text = sel;
        }
      });
      return cm;
    }

    void MenuItem(System.Windows.Controls.ContextMenu cm, string header, Action action) {
      var item = new System.Windows.Controls.MenuItem { Header = header };
      item.Click += (s, e) => action();
      cm.Items.Add(item);
    }

    // ── 파일 탐색 (핵심 수정) ────────────────────────────────
    // Document 객체 교체 대신 Text 내용만 교체
    // → SyntaxHighlighting / Options / FoldingManager 유지됨
    void OnNavigate(PlSqlFile file, int line) {
      if (!Dispatcher.CheckAccess()) {
        Dispatcher.Invoke(() => OnNavigate(file, line));
        return;
      }
      try {
        if (file.Doc == null)
          file.Doc = new ICSharpCode.AvalonEdit.Document.TextDocument(file.Content);

        // 내용이 다를 때만 Text 교체
        if (Editor.Document.Text != file.Doc.Text) {
          Editor.Document.Text = file.Doc.Text;
          // 뷰용 Doc을 에디터 Document로 통일
          file.Doc = Editor.Document;

          if (_foldMgr != null)
            _foldStrat?.UpdateFoldings(_foldMgr, Editor.Document);

          TbEditorTitle.Text = $"📝 {file.FileName}";
          LbFiles.SelectedItem = file;
        }

        // 한 프레임 뒤 스크롤 (렌더링 완료 후)
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded, () => {
              try {
                if (line < 1)
                  return;
                int safeL = Math.Min(line, Editor.Document.LineCount);
                Editor.ScrollToLine(safeL);
                var docLine = Editor.Document.GetLineByNumber(safeL);
                Editor.Select(docLine.Offset, docLine.Length);
                Editor.TextArea.Caret.BringCaretToView();
              }
              catch { }
            });
      }
      catch (Exception ex) { _vm.Status = $"탐색 오류: {ex.Message}"; }
    }

    // ── 단축키 ───────────────────────────────────────────────
    protected override void OnKeyDown(KeyEventArgs e) {
      base.OnKeyDown(e);
      if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        OpenFiles();
      if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        ShowFindBar();
      if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        PasteFromClipboard();
      if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control) {
        // 기본 붙여넣기는 AvalonEdit 이 처리하도록 두고
        // 한 프레임 뒤에 자동 분석 실행
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Input,
            () => AnalyzeEditorContent());
      }
      if (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        _vm.FormatCmd.Execute(null);
      if (e.Key == Key.Escape)
        FindBar.Visibility = Visibility.Collapsed;
    }

    // ── 파일 열기 ────────────────────────────────────────────
    void OpenFiles() {
      var dlg = new OpenFileDialog {
        Filter = "PL/SQL 파일|*.sql;*.pls;*.pkb;*.pks;*.pck;*.trg|모든 파일|*.*",
        Multiselect = true,
        Title = "PL/SQL 파일 선택"
      };
      if (dlg.ShowDialog() != true)
        return;
      foreach (var f in dlg.FileNames)
        _vm.LoadFile(f);
      UpdateFileCount();
    }

    void MenuOpenFile(object s, RoutedEventArgs e) => OpenFiles();
    void MenuExit(object s, RoutedEventArgs e) => Close();
    void MenuFind(object s, RoutedEventArgs e) => ShowFindBar();
    void MenuDeadCode(object s, RoutedEventArgs e) =>
        MessageBox.Show(
            "⚠ 표시 오브젝트 = 같은 파일 내에서 호출이 탐지되지 않음.\n" +
            "외부 패키지·스케줄러 호출이 있을 수 있으므로 참고용으로 사용하세요.",
            "Dead Code 안내", MessageBoxButton.OK, MessageBoxImage.Information);

    void MenuOpenFolder(object s, RoutedEventArgs e) {
      var dlg = new System.Windows.Forms.FolderBrowserDialog {
        Description = "SQL 파일이 있는 폴더를 선택하세요"
      };
      if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        return;
      var files = Directory.GetFiles(dlg.SelectedPath, "*.sql", SearchOption.AllDirectories)
          .Concat(Directory.GetFiles(dlg.SelectedPath, "*.pls", SearchOption.AllDirectories))
          .Concat(Directory.GetFiles(dlg.SelectedPath, "*.pkb", SearchOption.AllDirectories))
          .Concat(Directory.GetFiles(dlg.SelectedPath, "*.pks", SearchOption.AllDirectories));
      int cnt = 0;
      foreach (var f in files) {
        _vm.LoadFile(f);
        cnt++;
      }
      UpdateFileCount();
      _vm.Status = $"폴더에서 {cnt}개 파일 로드 완료";
    }

    void MenuHelp(object s, RoutedEventArgs e) =>
        MessageBox.Show(
            "PL/SQL Analyzer 사용법\n\n" +
            "1. 파일 열기 또는 .sql 파일 드래그&드롭\n" +
            "2. 왼쪽 트리에서 프로시저/함수 클릭 → 에디터 이동\n" +
            "3. [호출] 탭에서 호출 더블클릭 → 정의로 이동\n" +
            "4. [CRUD] 탭 → [빌드] 로 테이블 사용 현황 파악\n" +
            "5. Ctrl+Shift+V → 클립보드 PL/SQL 바로 분석\n\n" +
            "단축키:\n" +
            "  Ctrl+O           파일 열기\n" +
            "  Ctrl+F           에디터 내 찾기\n" +
            "  Ctrl+Shift+F     코드 포맷\n" +
            "  Ctrl+Shift+V     클립보드 붙여넣기\n" +
            "  ESC              찾기 바 닫기",
            "도움말", MessageBoxButton.OK, MessageBoxImage.Information);

    // ── 드래그 & 드롭 ────────────────────────────────────────
    void Window_DragOver(object s, DragEventArgs e) {
      e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
          ? DragDropEffects.Copy : DragDropEffects.None;
      e.Handled = true;
    }
    void Window_Drop(object s, DragEventArgs e) {
      if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        return;
      foreach (var f in (string[])e.Data.GetData(DataFormats.FileDrop))
        _vm.LoadFile(f);
      UpdateFileCount();
    }

    // ── 클립보드 붙여넣기 ────────────────────────────────────
    void PasteFromClipboard() {
      if (!Clipboard.ContainsText()) {
        _vm.Status = "클립보드에 텍스트가 없습니다.";
        return;
      }
      var text = Clipboard.GetText();
      if (string.IsNullOrWhiteSpace(text)) {
        _vm.Status = "클립보드 내용이 비어 있습니다.";
        return;
      }
      var fakeName = $"clipboard_{DateTime.Now:HHmmss}.sql";
      _vm.LoadContent(fakeName, text);
      UpdateFileCount();
    }

    // ── 에디터 현재 내용을 분석 ──────────────────────────
    void AnalyzeEditorContent() {
      var text = Editor.Document.Text;
      if (string.IsNullOrWhiteSpace(text)) {
        _vm.Status = "에디터가 비어 있습니다.";
        return;
      }

      // 기존 clipboard_/editor_ 파일 제거
      var cur = _vm.CurrentFile;
      if (cur != null && _vm.Files.Contains(cur) &&
          (cur.FilePath.StartsWith("clipboard_") ||
           cur.FilePath.StartsWith("editor_")))
        _vm.Files.Remove(cur);

      var fakeName = $"editor_{DateTime.Now:HHmmss}.sql";
      _vm.LoadContent(fakeName, text);
      UpdateFileCount();

      // ★ 분석 후 첫 번째 오브젝트 자동 선택 → 오른쪽 패널 채움
      var firstObj = _vm.CurrentFile?.Objects.FirstOrDefault(
          o => o.Kind == ObjType.Procedure || o.Kind == ObjType.Function)
          ?? _vm.CurrentFile?.Objects.FirstOrDefault();

      if (firstObj != null) {
        _vm.SelectedObject = firstObj;
        // 트리에서도 선택 표시
        firstObj.IsSelected = true;
        _vm.Status = $"분석 완료 — {firstObj.Name} 외 " +
                     $"{_vm.CurrentFile!.Objects.Count - 1}개 오브젝트";
      }
      else {
        _vm.Status = "오브젝트를 찾지 못했습니다. " +
                     "PROCEDURE/FUNCTION 선언이 포함된 코드인지 확인하세요.";
      }
    }

    // ── 트리 / 리스트 이벤트 ────────────────────────────────
    void ObjTree_SelectedItemChanged(object s,
        RoutedPropertyChangedEventArgs<object> e) {
      if (e.NewValue is not PlSqlObj obj)
        return;
      _vm.SelectedObject = obj;   // 우측 패널 갱신
      var file = _vm.Files.FirstOrDefault(f => f.FilePath == obj.FilePath);
      if (file != null)
        OnNavigate(file, obj.StartLine);
    }

    void ObjTree_DoubleClick(object s, MouseButtonEventArgs e) {
      if (ObjTree.SelectedItem is PlSqlObj obj) {
        _vm.SelectedObject = obj;
        var file = _vm.Files.FirstOrDefault(f => f.FilePath == obj.FilePath);
        if (file != null)
          OnNavigate(file, obj.StartLine);
      }
    }

    void LbFiles_SelectionChanged(object s,
        System.Windows.Controls.SelectionChangedEventArgs e) {
      if (LbFiles.SelectedItem is PlSqlFile pf) {
        _vm.CurrentFile = pf;
        OnNavigate(pf, 1);
      }
    }

    void LbSearch_DoubleClick(object s, MouseButtonEventArgs e) {
      if (LbSearch.SelectedItem is PlSqlObj obj) {
        _vm.SelectedObject = obj;
        var file = _vm.Files.FirstOrDefault(f => f.FilePath == obj.FilePath);
        if (file != null)
          OnNavigate(file, obj.StartLine);
      }
    }

    // ── SQL 상세 ─────────────────────────────────────────────
    void DgSqls_SelectionChanged(object s,
        System.Windows.Controls.SelectionChangedEventArgs e) {
      if (DgSqls.SelectedItem is not SqlInfo qi)
        return;
      LbSqlCols.ItemsSource = qi.Columns;
      var raw = qi.Raw?.Trim() ?? "";
      TbRawSql.Text = raw.Length > 2000 ? raw[..2000] + "\n...(truncated)" : raw;
      // 해당 SQL 라인으로 에디터 이동
      if (_vm.CurrentFile != null)
        OnNavigate(_vm.CurrentFile, qi.StartLine);
    }

    // ── 호출 탐색 ────────────────────────────────────────────
    void DgCalls_DoubleClick(object s, MouseButtonEventArgs e) {
      if (DgCalls.SelectedItem is CallRef cr)
        _vm.NavigateToCall(cr);
    }

    // ── 찾기 바 ──────────────────────────────────────────────
    void ShowFindBar() {
      FindBar.Visibility = Visibility.Visible;
      TbFindText.Focus();
      TbFindText.SelectAll();
    }
    void FindClose(object s, RoutedEventArgs e) =>
        FindBar.Visibility = Visibility.Collapsed;
    void TbFindText_KeyDown(object s, KeyEventArgs e) {
      if (e.Key == Key.Enter) {
        FindNextImpl(true);
        e.Handled = true;
      }
      if (e.Key == Key.Escape)
        FindBar.Visibility = Visibility.Collapsed;
    }
    void FindNext(object s, RoutedEventArgs e) => FindNextImpl(true);
    void FindPrev(object s, RoutedEventArgs e) => FindNextImpl(false);

    void FindNextImpl(bool forward) {
      var text = TbFindText.Text;
      if (string.IsNullOrEmpty(text))
        return;
      var src = Editor.Document.Text;
      if (string.IsNullOrEmpty(src))
        return;

      bool matchCase = CbMatchCase.IsChecked == true;
      bool useRegex = CbRegex.IsChecked == true;
      var sc = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
      int start = forward
          ? Editor.SelectionStart + (Editor.SelectionLength > 0 ? Editor.SelectionLength : 1)
          : Editor.SelectionStart - 1;
      if (start < 0)
        start = src.Length - 1;
      if (start >= src.Length)
        start = 0;

      int idx = -1;
      int len = text.Length;
      try {
        if (useRegex) {
          var ro = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
          var rx = new Regex(text, ro);
          var ms = rx.Matches(src);
          var m = forward
              ? ms.Cast<Match>().FirstOrDefault(m => m.Index >= start)
                ?? ms.Cast<Match>().FirstOrDefault()
              : ms.Cast<Match>().LastOrDefault(m => m.Index < start)
                ?? ms.Cast<Match>().LastOrDefault();
          if (m != null) {
            idx = m.Index;
            len = m.Length;
          }
        }
        else {
          idx = forward
              ? src.IndexOf(text, start, sc)
              : src.LastIndexOf(text, start, sc);
          if (idx < 0)
            idx = forward
              ? src.IndexOf(text, 0, sc)
              : src.LastIndexOf(text, sc);
        }
      }
      catch { TbFindStatus.Text = "정규식 오류"; return; }

      if (idx >= 0) {
        Editor.Select(idx, len);
        Editor.ScrollToLine(Editor.Document.GetLineByOffset(idx).LineNumber);
        TbFindStatus.Text = $"L{Editor.Document.GetLineByOffset(idx).LineNumber}";
      }
      else
        TbFindStatus.Text = "찾을 수 없음";
    }

    // ── 상태바 파일 카운트 ───────────────────────────────────
    void UpdateFileCount() {
      TbFileCount.Text =
          $"파일 {_vm.Files.Count}개  |  " +
          $"오브젝트 {_vm.Files.Sum(f => f.Objects.Count)}개";
    }
  }

  // ── PlSqlFolding ─────────────────────────────────────────────
  public class PlSqlFolding {
    static readonly Regex RxBegin = new(
        @"\b(BEGIN|IF|LOOP|CASE|DECLARE)\b", RegexOptions.IgnoreCase);
    static readonly Regex RxEnd = new(
        @"\bEND\b", RegexOptions.IgnoreCase);

    public void UpdateFoldings(FoldingManager? manager,
                               ICSharpCode.AvalonEdit.Document.TextDocument? doc) {
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
        if (manager.AllFoldings != null)
          manager.UpdateFoldings(foldings, -1);
      }
      catch { }
    }
  }
}