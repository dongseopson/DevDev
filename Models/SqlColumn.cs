using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class SqlColumn {
    public string Expr { get; set; } = "";
    public string? Alias {
      get; set;
    }
    public bool IsSubquery {
      get; set;
    }
    public string Display => IsSubquery ? $"[SUB] {(Alias ?? Expr)}" : (Alias != null ? $"{Expr}  AS  {Alias}" : Expr);
  }
}