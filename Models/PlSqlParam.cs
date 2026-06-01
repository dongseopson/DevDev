using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class PlSqlParam {
    public string Name { get; set; } = "";
    public string DataType { get; set; } = "";
    public string Dir { get; set; } = "IN";
    public string? Default {
      get; set;
    }
    public bool NoCopy {
      get; set;
    }
    public int Line {
      get; set;
    }
  }
}