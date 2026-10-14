using System.Windows.Automation;
using System.Windows.Data;
using Path = System.Windows.Shapes.Path;

namespace Lutra.App;

internal static class Ui
{
    public static string Language { get; set; } = "en";
    public static string T(string en, string fi) => Language == "fi" ? fi : en;
    public static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    public static TextBlock Text(string text, double size = 13, string? color = null, bool bold = false)
        => new() { Text = text, FontSize = size, Foreground = Color(color ?? "#202A28"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        if (primary) button.Style = (Style)Application.Current.FindResource("Primary");
        button.Click += (_, _) => action(); AutomationProperties.SetName(button, text); return button;
    }
    public static Border Panel(UIElement child, Thickness? padding = null)
        => new() { Background = Brushes.White, BorderBrush = Color("#D9DFDC"), BorderThickness = new(1), Padding = padding ?? new Thickness(20), Child = child };
    public static StackPanel Stack(params UIElement[] children)
    {
        var stack = new StackPanel(); foreach (var c in children) stack.Children.Add(c); return stack;
    }
    public static StackPanel Row(params UIElement[] children)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var c in children) stack.Children.Add(c); return stack;
    }
    public static FrameworkElement Field(string label, FrameworkElement control)
    {
        AutomationProperties.SetName(control, label);
        var caption = new Label { Content = label, Target = control, Foreground = Color("#596763"), Padding = new(0, 0, 0, 6), FontSize = 12 };
        return new StackPanel { Margin = new(0, 0, 0, 13), Children = { caption, control } };
    }
    public static TextBox Input(string text) => new() { Text = text, MinWidth = 60 };
    public static TextBox Number(double number) => Input(number.ToString("0.##", CultureInfo.CurrentCulture));
    public static double Number(TextBox box)
    {
        if (!double.TryParse(box.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new InvalidDataException(T("Enter a valid number.", "Anna kelvollinen numero."));
        return value;
    }
    public static Grid Columns(params (double Width, UIElement Element)[] children)
    {
        var grid = new Grid();
        for (int i = 0; i < children.Length; i++)
        {
            grid.ColumnDefinitions.Add(new() { Width = children[i].Width > 0 ? new(children[i].Width) : new(1, GridUnitType.Star) });
            Grid.SetColumn(children[i].Element, i); grid.Children.Add(children[i].Element);
        }
        return grid;
    }
    public static DataGrid Table(params (string Header, string Binding, double Width)[] columns)
    {
        var grid = new DataGrid();
        foreach (var col in columns) grid.Columns.Add(new DataGridTextColumn { Header = col.Header, Binding = new Binding(col.Binding),
            Width = col.Width > 0 ? new(col.Width) : new(1, DataGridLengthUnitType.Star) });
        return grid;
    }
    public static FrameworkElement Mark(double size = 30)
    {
        var canvas = new Canvas { Width = size, Height = size };
        var view = new Viewbox { Width = size, Height = size };
        var geometry = new Canvas { Width = 32, Height = 32 };
        geometry.Children.Add(new Path { Data = Geometry.Parse("M7,5 L7,20 Q7,25 12,25 L27,25"), Stroke = Color("#99CDB7"), StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        geometry.Children.Add(new Path { Data = Geometry.Parse("M16,5 L16,15 Q16,17 18,17 L27,17"), Stroke = Brushes.White, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        view.Child = geometry; canvas.Children.Add(view); return canvas;
    }
}

