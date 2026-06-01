using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PlSqlAnalyzer.Models {
  public class Metrics {
    public int Total {
      get; set;
    }
    public int Code {
      get; set;
    }
    public int Comments {
      get; set;
    }
    public int Blank {
      get; set;
    }
    public int Complexity { get; set; } = 1;
    public int MaxNesting {
      get; set;
    }
    public int Params {
      get; set;
    }
    public int SqlCount {
      get; set;
    }
    public int CallCount {
      get; set;
    }
    public int VarCount {
      get; set;
    }

    public string ComplexityLabel => Complexity switch {
      <= 5 => "낮음 (Good)",
      <= 10 => "보통",
      <= 20 => "⚠ 높음",
      _ => "🔴 매우 높음 (리팩터링 권장)"
    };
    public string ParamLabel => Params switch {
      <= 5 => $"{Params}",
      <= 10 => $"⚠ {Params} (많음)",
      _ => $"🔴 {Params} (과다)"
    };
  }
}