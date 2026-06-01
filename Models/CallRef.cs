using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class CallRef {
    public string? PkgName {
      get; set;
    }
    public string Name { get; set; } = "";
    public int Line {
      get; set;
    }
    public PlSqlObj? Target {
      get; set;
    }
    public string FullName => PkgName != null ? $"{PkgName}.{Name}" : Name;
    public string Status => Target != null ? "✔" : "?";
    public string Display => $"L{Line,5}  {(PkgName != null ? PkgName + "." : "")}{Name}";
  }
}