using System.Windows;
using System.Windows.Media;

namespace Anjana.Controls;

/// <summary>Lightweight area sparkline; history scrolls in from the right edge.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values { get => (IReadOnlyList<double>?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Brush? Stroke { get => (Brush?)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    public int Capacity { get; set; } = 60;
    /// <summary>Floor for the vertical scale so idle noise doesn't look like a spike (bytes/s).</summary>
    public double MinScale { get; set; } = 128 * 1024;

    protected override void OnRender(DrawingContext dc)
    {
        var v = Values;
        double w = ActualWidth, h = ActualHeight;
        if (v is null || v.Count < 2 || Stroke is null || w <= 0 || h <= 0) return;

        const double pad = 1.5;
        double max = Math.Max(v.Max(), MinScale);
        double step = w / (Capacity - 1);
        double x0 = w - (v.Count - 1) * step;

        var pts = new List<Point>(v.Count);
        for (int i = 0; i < v.Count; i++)
            pts.Add(new Point(x0 + i * step, h - pad - v[i] / max * (h - 2 * pad)));

        var area = new StreamGeometry();
        using (var c = area.Open())
        {
            c.BeginFigure(new Point(pts[0].X, h), true, true);
            c.PolyLineTo(pts, false, false);
            c.LineTo(new Point(pts[^1].X, h), false, false);
        }
        area.Freeze();

        var line = new StreamGeometry();
        using (var c = line.Open())
        {
            c.BeginFigure(pts[0], false, false);
            c.PolyLineTo(pts.Skip(1).ToList(), true, true);
        }
        line.Freeze();

        var fill = Stroke.CloneCurrentValue();
        fill.Opacity = 0.16;
        var pen = new Pen(Stroke, 1.5) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        dc.PushClip(new RectangleGeometry(new Rect(0, 0, w, h)));
        dc.DrawGeometry(fill, null, area);
        dc.DrawGeometry(null, pen, line);
        dc.Pop();
    }
}
