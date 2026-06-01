using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

using PlSqlAnalyzer.Models;

namespace PlSqlAnalyzer.Converters {
  public class BoolToVis : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class BoolToVisInv : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class NullToVis : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v != null ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class StringToColor : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c) {
      try {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(v?.ToString() ?? "#D4D4D4"));
      }
      catch { return Brushes.Gray; }
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class DeadCodeColor : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? new SolidColorBrush(Color.FromRgb(0x80, 0x40, 0x40))
                     : new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class CallResolvedColor : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v != null ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0))
                   : new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
  public class UnusedVarColor : IValueConverter {
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is false ? new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x40))
                      : Brushes.Transparent;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => DependencyProperty.UnsetValue;
  }
}
