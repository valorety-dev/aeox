using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Aeox.App.Controls;

public sealed class FpsChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(FpsChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarksProperty = DependencyProperty.Register(
        nameof(Marks), typeof(IReadOnlyList<int>), typeof(FpsChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IReadOnlyList<int>? Marks
    {
        get => (IReadOnlyList<int>?)GetValue(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var line = (Brush)FindResource("LineBrush");
        var faint = (Brush)FindResource("FaintBrush");
        var accent = (SolidColorBrush)FindResource("AccentBrush");
        var ink = (SolidColorBrush)FindResource("TextBrush");
        var soft = (Brush)FindResource("AccentSoftBrush");
        var typeface = new Typeface((FontFamily)FindResource("UiFont"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        var top = 18.0;
        var bottom = h - 1;
        dc.DrawRectangle(line, null, new Rect(0, bottom, w, 1));

        var values = Values;
        if (values is null || values.Count == 0)
        {
            var empty = new FormattedText("No matches recorded yet", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 12.5, faint, dpi);
            dc.DrawText(empty, new Point((w - empty.Width) / 2, (h - empty.Height) / 2));
            return;
        }

        var max = Math.Max(values.Max() * 1.08, 60);
        var slot = w / values.Count;
        var barW = Math.Max(2, Math.Min(18, slot * 0.62));
        Rect? labelRect = null;
        FormattedText? labelText = null;
        var labelPoint = new Point();
        var gridValue = Math.Round(max / 2 / 50) * 50;
        if (gridValue > 0)
        {
            var gy = bottom - (bottom - top) * gridValue / max;
            dc.DrawRectangle(line, null, new Rect(0, gy, w, 1));
            var label = new FormattedText($"{gridValue:0} fps", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 10.5, faint, dpi);
            var lp = new Point(w - label.Width - 4, gy - label.Height / 2);
            dc.DrawRectangle((Brush)FindResource("WindowBrush"), null, new Rect(lp.X - 6, lp.Y - 1, label.Width + 10, label.Height + 2));
            labelRect = new Rect(lp.X - 6, lp.Y - 1, label.Width + 10, label.Height + 2);
            labelText = label;
            labelPoint = lp;
        }

        var marks = Marks ?? Array.Empty<int>();
        var dashed = new Pen(accent, 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
        foreach (var m in marks)
        {
            if (m <= 0 || m >= values.Count) continue;
            var x = Math.Round(m * slot) + 0.5;
            dc.DrawLine(dashed, new Point(x, top - 4), new Point(x, bottom));
        }

        var dim = new SolidColorBrush(Color.FromArgb(120, ink.Color.R, ink.Color.G, ink.Color.B));
        dim.Freeze();
        for (var i = 0; i < values.Count; i++)
        {
            var barH = Math.Max(2, (bottom - top) * values[i] / max);
            var x = i * slot + (slot - barW) / 2;
            var brush = i == values.Count - 1 ? accent : dim;
            dc.DrawRectangle(brush, null, new Rect(x, bottom - barH, barW, barH));
        }

        if (labelRect is { } lr && labelText is not null)
        {
            dc.DrawRectangle((Brush)FindResource("WindowBrush"), null, lr);
            dc.DrawText(labelText, labelPoint);
        }
    }
}
