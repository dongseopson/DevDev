using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;

using ICSharpCode.AvalonEdit.Document;

using PlSqlAnalyzer.Models;
using PlSqlAnalyzer.Parser;

namespace PlSqlAnalyzer.ViewModels {
  public class CrudRow {
    public string Name { get; set; } = "";
    public string Package { get; set; } = "";
    public HashSet<string> Selects { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Inserts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Updates { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Deletes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Merges { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool HasAny => Selects.Count + Inserts.Count + Updates.Count
                             + Deletes.Count + Merges.Count > 0;
    public string FullName => Package.Length > 0 ? $"{Package}.{Name}" : Name;
    public string SelectStr => string.Join(", ", Selects);
    public string InsertStr => string.Join(", ", Inserts);
    public string UpdateStr => string.Join(", ", Updates);
    public string DeleteStr => string.Join(", ", Deletes);
    public string MergeStr => string.Join(", ", Merges);
  }
}

