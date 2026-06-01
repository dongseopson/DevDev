using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class PlSqlVar {
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public bool IsConst {
      get; set;
    }
    public bool IsExcept {
      get; set;
    }
    public string? Default {
      get; set;
    }
    public int Line {
      get; set;
    }
    public bool IsUsed { get; set; } = true;
    public string Kind => IsExcept ? "EXCEPTION" : IsConst ? "CONSTANT" : "VARIABLE";
  }
}