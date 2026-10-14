using System.Windows.Automation;
using System.Windows.Data;
using Path = System.Windows.Shapes.Path;

namespace Lutra.App;

internal static class Ui
{
    public const string WorkspaceHex = "#101715", SurfaceHex = "#18221F", RaisedHex = "#202D28";
    public const string InkHex = "#ECF3EF", MutedHex = "#A0B2A8", AccentHex = "#72D3AE", LineHex = "#304139";
    public const string WaterHex = "#6DC5EA", GpuHex = "#E4C584", WarningHex = "#E4C584", DangerHex = "#F29A8F";
    public static readonly Brush Workspace = Color(WorkspaceHex), Surface = Color(SurfaceHex), Raised = Color(RaisedHex);
    public static readonly Brush Ink = Color(InkHex), Muted = Color(MutedHex), Accent = Color(AccentHex), Line = Color(LineHex);
    public static readonly Brush Water = Color(WaterHex), Gpu = Color(GpuHex), Warning = Color(WarningHex), Danger = Color(DangerHex);
    public static string Language { get; set; } = "en";
    public static string T(string en, string fi) => Language == "fi" ? fi : en;
    public static Brush Color(string hex)
    {
        // Keep older surfaces on the same palette while their semantic tokens are adopted.
        string value = hex.ToUpperInvariant() switch
        {
            "#F0F2F0" => WorkspaceHex,
            "#FFFFFF" or "WHITE" or "#202A28" => InkHex,
            "#596763" or "#B4C6BE" => MutedHex,
            "#256450" or "#287F62" or "#99CDB7" => AccentHex,
            "#D9DFDC" => LineHex,
            "#DFEBE4" or "#43564F" => "#2B4639",
            "#EFF3F0" or "#F7F9F7" => RaisedHex,
            "#252E2C" => "#0C1210",
            "#D0DDD7" => "#C1CFC7",
            "#854D12" or "#8B4D19" => WarningHex,
            "#9B352C" => DangerHex,
            _ => hex
        };
        var brush = (Brush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze(); return brush;
    }
    public static TextBlock Text(string text, double size = 13, string? color = null, bool bold = false)
        => new() { Text = text, FontSize = size, Foreground = color == null ? Ink : Color(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action, bool primary = false)
    {
        var button = new Button { Content = text, Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        if (primary) button.Style = (Style)Application.Current.FindResource("Primary");
        button.Click += (_, _) => action(); AutomationProperties.SetName(button, text); return button;
    }
    public static Border Panel(UIElement child, Thickness? padding = null)
        => new() { Background = Surface, BorderBrush = Line, BorderThickness = new(1), Padding = padding ?? new Thickness(20), Child = child };
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
        var caption = new Label { Content = label, Target = control, Foreground = Muted, Padding = new(0, 0, 0, 6), FontSize = 12 };
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
        geometry.Children.Add(new Path { Data = Geometry.Parse("M7,5 L7,20 Q7,25 12,25 L27,25"), Stroke = Accent, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        geometry.Children.Add(new Path { Data = Geometry.Parse("M16,5 L16,15 Q16,17 18,17 L27,17"), Stroke = Ink, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
        view.Child = geometry; canvas.Children.Add(view); return canvas;
    }
}

