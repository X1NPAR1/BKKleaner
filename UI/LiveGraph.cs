using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace BKKleaner.UI;

/// <summary>
/// Lightweight real-time line graph rendered with a StreamGeometry.
/// GPU-accelerated by WPF's retained-mode renderer; no per-frame allocations
/// beyond the geometry itself.
/// </summary>
public sealed class LiveGraph : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable<double>), typeof(LiveGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(LiveGraph),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(LiveGraph),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
        nameof(MaxValue), typeof(double), typeof(LiveGraph),
        new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>Upper bound of the Y axis; 0 enables auto-scaling.</summary>
    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var graph = (LiveGraph)d;
        if (e.OldValue is INotifyCollectionChanged oldIncc)
            oldIncc.CollectionChanged -= graph.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newIncc)
            newIncc.CollectionChanged += graph.OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(InvalidateVisual);

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 2 || height <= 2) return;

        var values = Values?.ToArray();
        if (values is null || values.Length < 2) return;

        var max = MaxValue > 0 ? MaxValue : Math.Max(1, values.Max() * 1.1);
        var stepX = width / (values.Length - 1);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var first = new Point(0, Y(values[0], max, height));
            if (Fill is not null)
            {
                ctx.BeginFigure(new Point(0, height), isFilled: true, isClosed: true);
                ctx.LineTo(first, false, false);
                for (var i = 1; i < values.Length; i++)
                    ctx.LineTo(new Point(i * stepX, Y(values[i], max, height)), false, false);
                ctx.LineTo(new Point(width, height), false, false);
            }
            else
            {
                ctx.BeginFigure(first, isFilled: false, isClosed: false);
                for (var i = 1; i < values.Length; i++)
                    ctx.LineTo(new Point(i * stepX, Y(values[i], max, height)), true, true);
            }
        }
        geometry.Freeze();

        if (Fill is not null)
            dc.DrawGeometry(Fill, null, geometry);

        // Stroke pass over the same points.
        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(new Point(0, Y(values[0], max, height)), false, false);
            for (var i = 1; i < values.Length; i++)
                ctx.LineTo(new Point(i * stepX, Y(values[i], max, height)), true, true);
        }
        line.Freeze();
        dc.DrawGeometry(null, new Pen(Stroke, 1.6), line);
    }

    private static double Y(double value, double max, double height) =>
        height - Math.Clamp(value / max, 0, 1) * (height - 2) - 1;
}
