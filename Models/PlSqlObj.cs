using System.Collections.ObjectModel;
using System.ComponentModel;

using PlSqlAnalyzer.Models;

public class PlSqlObj : INotifyPropertyChanged {
  private bool _expanded, _selected;

  public string Name { get; set; } = "";
  public ObjType Kind {
    get; set;
  }
  public string? ReturnType {
    get; set;
  }
  public int StartLine {
    get; set;
  }
  public int EndLine {
    get; set;
  }
  public string FilePath { get; set; } = "";
  public string? Source {
    get; set;
  }
  public PlSqlObj? Parent {
    get; set;
  }
  public bool IsNeverCalled {
    get; set;
  }

  public List<PlSqlParam> Params { get; set; } = new();
  public List<SqlInfo> Sqls { get; set; } = new();
  public List<CallRef> Calls { get; set; } = new();
  public List<PlSqlVar> Vars { get; set; } = new();
  public ObservableCollection<PlSqlObj> Children { get; set; } = new();
  public Metrics Metrics { get; set; } = new();

  public bool IsExpanded {
    get => _expanded;
    set {
      _expanded = value;
      PC(nameof(IsExpanded));
    }
  }
  public bool IsSelected {
    get => _selected;
    set {
      _selected = value;
      PC(nameof(IsSelected));
    }
  }

  public string Icon => Kind switch {
    ObjType.Package or ObjType.PackageBody => "PKG",
    ObjType.Procedure => "PRC",
    ObjType.Function => "FNC",
    ObjType.Cursor => "CUR",
    ObjType.TypeDef => "TYP",
    ObjType.Trigger => "TRG",
    _ => "???"
  };
  public string IconColor => Kind switch {
    ObjType.Package or ObjType.PackageBody => "#E8A838",
    ObjType.Procedure => "#4FC1FF",
    ObjType.Function => "#C586C0",
    ObjType.Cursor => "#9CDCFE",
    ObjType.TypeDef => "#4EC9B0",
    ObjType.Trigger => "#F44747",
    _ => "#808080"
  };
  public string DisplayName => Kind == ObjType.Function
      ? $"{Name}  : {ReturnType ?? "?"}"
      : Name;
  public string LineInfo =>
      $"L{StartLine}~{EndLine} ({EndLine - StartLine + 1}줄)";

  public event PropertyChangedEventHandler? PropertyChanged;
  void PC(string n) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}