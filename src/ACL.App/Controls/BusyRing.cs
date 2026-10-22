using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace ACL.App.Controls;

/// <summary>
/// 自绘环形加载指示器（不依赖 Avalonia 的动画器系统，因此不会触发
/// "TransformAnimator 无法转换"类崩溃；角度由计时器推进）。
/// </summary>
public sealed class BusyRing : Control
{
    /// <summary>环颜色。</summary>
    public static readonly StyledProperty<IBrush?> RingBrushProperty =
        AvaloniaProperty.Register<BusyRing, IBrush?>(nameof(RingBrush));

    /// <summary>进度弧颜色。</summary>
    public static readonly StyledProperty<IBrush?> ArcBrushProperty =
        AvaloniaProperty.Register<BusyRing, IBrush?>(nameof(ArcBrush));

    /// <summary>线宽。</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<BusyRing, double>(nameof(Thickness), 3.4);

    /// <summary>每圈毫秒数。</summary>
    public static readonly StyledProperty<double> SpeedMsProperty =
        AvaloniaProperty.Register<BusyRing, double>(nameof(SpeedMs), 1100d);

    /// <summary>是否旋转。</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<BusyRing, bool>(nameof(IsRunning), true);

    private readonly DispatcherTimer _timer;
    private double _angle;
    private DateTime _last = DateTime.UtcNow;

    /// <summary>构造环形指示器。</summary>
    public BusyRing()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, OnTick);
    }

    /// <summary>环颜色。</summary>
    public IBrush? RingBrush
    {
        get => GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    /// <summary>进度弧颜色。</summary>
    public IBrush? ArcBrush
    {
        get => GetValue(ArcBrushProperty);
        set => SetValue(ArcBrushProperty, value);
    }

    /// <summary>线宽。</summary>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <summary>每圈毫秒数。</summary>
    public double SpeedMs
    {
        get => GetValue(SpeedMsProperty);
        set => SetValue(SpeedMsProperty, value);
    }

    /// <summary>是否旋转。</summary>
    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _last = DateTime.UtcNow;
        _timer.Start();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!IsRunning || Bounds.Width <= 1)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        double delta = Math.Clamp((now - _last).TotalMilliseconds, 0, 64);
        _last = now;
        double speed = Math.Max(240, SpeedMs);
        _angle = (_angle + (delta / speed * 360d)) % 360d;
        InvalidateVisual();
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        Rect bounds = Bounds;
        double size = Math.Min(bounds.Width, bounds.Height);
        if (size <= 2)
        {
            return;
        }

        double radius = (size - Thickness) / 2;
        Point center = new(bounds.Width / 2, bounds.Height / 2);
        IBrush ring = RingBrush ?? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        IBrush arc = ArcBrush ?? new SolidColorBrush(Color.FromArgb(255, 120, 160, 255));

        context.DrawEllipse(null, new Pen(ring, Thickness), center, radius, radius);
        context.DrawGeometry(null, new Pen(arc, Thickness, lineCap: PenLineCap.Round), Arc(center, radius, _angle, 96));
        context.DrawGeometry(null, new Pen(arc, Math.Max(1, Thickness - 1.2), lineCap: PenLineCap.Round), Arc(center, radius, _angle + 158, 46));
    }

    private static Geometry Arc(Point center, double radius, double startDegrees, double sweepDegrees)
    {
        double start = startDegrees * Math.PI / 180d;
        double end = (startDegrees + sweepDegrees) * Math.PI / 180d;
        Point from = new(center.X + (radius * Math.Cos(start)), center.Y + (radius * Math.Sin(start)));
        Point to = new(center.X + (radius * Math.Cos(end)), center.Y + (radius * Math.Sin(end)));

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(from, isFilled: false);
            ctx.ArcTo(to, new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise);
        }

        return geometry;
    }
}