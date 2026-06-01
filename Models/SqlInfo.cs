using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class SqlInfo {
    public string Kind { get; set; } = "SELECT";
    public string Raw { get; set; } = "";
    public List<SqlColumn> Columns { get; set; } = new();
    public List<string> Tables { get; set; } = new();
    public List<string> FromSubqueries { get; set; } = new();
    public List<string> WhereSubqueries { get; set; } = new();
    public string? Where {
      get; set;
    }
    public int StartLine {
      get; set;
    }
    public int EndLine {
      get; set;
    }
    public string? CursorName {
      get; set;
    }
    public string Display => $"[{Kind,-6}] L{StartLine,5}  Tables:{string.Join(",", Tables)}";
  }  
}