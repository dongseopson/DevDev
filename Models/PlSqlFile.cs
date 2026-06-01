using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class PlSqlFile {
    public string FilePath { get; set; } = "";
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public string Content { get; set; } = "";
    public List<PlSqlObj> Objects { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public ICSharpCode.AvalonEdit.Document.TextDocument? Doc {
      get; set;
    }
  }
}