using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Lutra.App;

internal class Plot : FrameworkElement
{
    public static readonly Brush Green = Ui.Accent;
    protected static readonly Brush Muted = Ui.Muted;
    protected Rect Area => new(43, 20, Math.Max(1, ActualWidth - 62), Math.Max(1, ActualHeight - 53));
    protected void Text(DrawingContext dc, string text, double x, double y, Brush? ink = null)
        => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, ink ?? Muted, VisualTreeHelper.GetDpi(this).PixelsPerDip), new(x, y));
    protected void Axes(DrawingContext dc, double minimum, double maximum, string unit)
    {
        dc.DrawRectangle(Ui.Surface, null, new(0, 0, ActualWidth, ActualHeight));
        var grid = new Pen(Ui.Line, 1);
        for (int i = 0; i <= 4; i++)
        {
            double y = Area.Bottom - Area.Height * i / 4;
            dc.DrawLine(grid, new(Area.Left, y), new(Area.Right, y));
            Text(dc, (minimum + (maximum - minimum) * i / 4).ToString("0.#"), 3, y - 7);
        }
        Text(dc, unit, 3, 1);
    }
}
internal sealed class CurvePlot : Plot
{
    public CurvePoint[] Points { get; private set; } = [];
    public double Startup { get; set; } = 40;
    public double? LiveTemperature { get; set; }
    public int Selected { get; set; }
    public event Action? Edited;
    public event Action? SelectionChanged;
    private double low = 20, high = 60;
    private bool dragging;
    public CurvePlot()
    {
        MinHeight = 240; Focusable = true; Cursor = Cursors.Cross;
        AutomationProperties.SetName(this, Ui.T("Curve editor", "Käyräeditori"));
        AutomationProperties.SetHelpText(this, Ui.T("Left and right select a point; up and down change response. You can also select a point and edit its numeric coordinates below.", "Vasen ja oikea valitsevat pisteen, ylös ja alas muuttavat vastetta. Voit myös valita pisteen ja muokata sen koordinaatteja alapuolelta."));
    }
    protected override AutomationPeer OnCreateAutomationPeer() => new CurvePeer(this);
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }
    public void Set(CurvePoint[] points)
    {
        Points = points.ToArray(); low = Math.Max(0, Math.Floor((points[0].Temperature - 3) / 5) * 5);
        high = Math.Min(100, Math.Ceiling((points[^1].Temperature + 3) / 5) * 5);
        InvalidateVisual();
    }
    private Point Position(CurvePoint p) => new(Area.Left + (p.Temperature - low) / (high - low) * Area.Width, Area.Bottom - p.Power / 100 * Area.Height);
    protected override void OnRender(DrawingContext dc)
    {
        Axes(dc, 0, 100, "%");
        if (IsKeyboardFocused) dc.DrawRectangle(null, new Pen(Green, 2) { DashStyle = DashStyles.Dash }, new Rect(1, 1, Math.Max(1, ActualWidth - 2), Math.Max(1, ActualHeight - 2)));
        for (int i = 0; i <= 5; i++) Text(dc, (low + (high - low) * i / 5).ToString("0.#") + "°", Area.Left + Area.Width * i / 5 - 6, Area.Bottom + 10);
        if (Points.Length == 0) return;
        double x = Area.Left + (Startup - low) / (high - low) * Area.Width;
        if (x >= Area.Left && x <= Area.Right)
        {
            var dashed = new Pen(Muted, 1) { DashStyle = DashStyles.Dash };
            dc.DrawLine(dashed, new(x, Area.Top), new(x, Area.Bottom));
            Text(dc, Ui.T("Start ", "Käynnistys ") + Startup.ToString("0.##") + "°", Math.Clamp(x + 5, Area.Left, Area.Right - 110), Area.Top + 3);
        }
        var line = new Pen(Green, 2.5);
        for (int i = 1; i < Points.Length; i++) dc.DrawLine(line, Position(Points[i - 1]), Position(Points[i]));
        for (int i = 0; i < Points.Length; i++)
            dc.DrawEllipse(i == Selected ? Green : Ui.Surface, new(Green, 1.5), Position(Points[i]), i == Selected ? 5 : 3.5, i == Selected ? 5 : 3.5);
        if (LiveTemperature is { } t && t >= low && t <= high)
        {
            double liveX = Area.Left + (t - low) / (high - low) * Area.Width;
            dc.DrawLine(new Pen(Ui.Gpu, 1), new(liveX, Area.Top + 28), new(liveX, Area.Bottom));
            Text(dc, Ui.T("Live ", "Nyt ") + t.ToString("0.#") + "°", Math.Clamp(liveX + 6, Area.Left, Area.Right - 74), Area.Top + 22, Ui.Gpu);
            dc.DrawEllipse(Ui.Gpu, null, new(liveX, Area.Bottom), 3, 3);
        }
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Points.Length == 0) return;
        Focus(); var at = e.GetPosition(this);
        Selected = Enumerable.Range(0, Points.Length).MinBy(i => (Position(Points[i]) - at).Length);
        SelectionChanged?.Invoke(); InvalidateVisual();
        if ((Position(Points[Selected]) - at).Length <= 20) { dragging = true; CaptureMouse(); }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var at = e.GetPosition(this);
        Change(low + (at.X - Area.Left) / Area.Width * (high - low), (Area.Bottom - at.Y) / Area.Height * 100);
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging = false; ReleaseMouseCapture(); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Points.Length == 0) return;
        if (e.Key == Key.Left) Selected = Math.Max(0, Selected - 1);
        else if (e.Key == Key.Right) Selected = Math.Min(15, Selected + 1);
        else if (e.Key is Key.Up or Key.Down) Change(Points[Selected].Temperature, Points[Selected].Power + (e.Key == Key.Up ? 1 : -1));
        else return;
        e.Handled = true; SelectionChanged?.Invoke(); InvalidateVisual();
    }
    public void Change(double t, double power)
    {
        int i = Selected;
        double minT = i == 0 ? 0 : Points[i - 1].Temperature + .01, maxT = i == 15 ? 100 : Points[i + 1].Temperature - .01;
        double minP = i == 0 ? 0 : Points[i - 1].Power, maxP = i == 15 ? 100 : Points[i + 1].Power;
        Points[i] = new(Math.Round(Math.Clamp(t, minT, maxT), 2), Math.Round(Math.Clamp(power, minP, maxP), 2));
        Edited?.Invoke(); SelectionChanged?.Invoke(); InvalidateVisual();
    }
    private sealed class CurvePeer(CurvePlot owner) : FrameworkElementAutomationPeer(owner), IValueProvider
    {
        protected override string GetClassNameCore() => "LutraCurveEditor";
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
        protected override bool IsControlElementCore() => true;
        public override object? GetPattern(PatternInterface patternInterface) => patternInterface == PatternInterface.Value ? this : base.GetPattern(patternInterface);
        public bool IsReadOnly => true;
        public string Value => owner.Points.Length == 0 ? "" : Ui.T("Point ", "Piste ") + (owner.Selected + 1) + ": " + owner.Points[owner.Selected].Temperature.ToString("0.##") + " °C, " + owner.Points[owner.Selected].Power.ToString("0.##") + "%";
        public void SetValue(string value) => throw new InvalidOperationException("Use the labelled point coordinate fields.");
    }
}
internal sealed class HistoryPlot : Plot
{
    public HistoryPoint[] Points { get; set; } = [];
    public string Unit { get; set; } = "°C";
    public string Empty { get; set; } = "";
    protected override void OnRender(DrawingContext dc)
    {
        var valid = Points.Where(p => p.Value.HasValue).ToArray();
        double min = valid.Length == 0 ? 0 : Math.Max(0, Math.Floor(valid.Min(p => p.Value!.Value) / 5) * 5 - 5);
        double max = valid.Length == 0 ? 50 : Math.Max(min + 10, Math.Ceiling(valid.Max(p => p.Value!.Value) / 5) * 5 + 5);
        Axes(dc, min, max, Unit);
        if (valid.Length < 2) { Text(dc, Empty, Area.Left + 16, Area.Top + 28); return; }
        var from = Points[0].Time; var to = Points[^1].Time;
        double duration = Math.Max(1, (to - from).TotalSeconds);
        for (int i = 0; i <= 4; i++)
        {
            var time = from.AddSeconds(duration * i / 4).ToLocalTime();
            Text(dc, time.ToString(duration > 86400 ? "dd MMM" : "HH:mm"), Area.Left + Area.Width * i / 4 - 10, Area.Bottom + 10);
        }
        Point? previous = null; var pen = new Pen(Green, 2);
        foreach (var p in Points)
        {
            if (!p.Value.HasValue) { previous = null; continue; }
            Point next = new(Area.Left + (p.Time - from).TotalSeconds / duration * Area.Width, Area.Bottom - (p.Value.Value - min) / (max - min) * Area.Height);
            if (previous.HasValue) dc.DrawLine(pen, previous.Value, next);
            previous = next;
        }
    }
}

