using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ACL.App.Controls;

/// <summary>
/// 平滑滚动容器（对齐参考实现的滚动手感）：拦截滚轮事件，把目标偏移累积后以 300ms 缓出动画过渡；
/// 新输入会从当前偏移重新出发（可打断、不跳变）。指针位于多行文本框 / 下拉框弹层时不接管。
/// </summary>
public class SmoothScrollViewer : ScrollViewer
{
    /// <summary>滚轮一格对应的像素数。</summary>
    public static readonly StyledProperty<double> WheelStepProperty =
        AvaloniaProperty.Register<SmoothScrollViewer, double>(nameof(WheelStep), 64d);

    /// <summary>动画时长（毫秒）。</summary>
    public static readonly StyledProperty<double> AnimationDurationProperty =
        AvaloniaProperty.Register<SmoothScrollViewer, double>(nameof(AnimationDuration), 300d);

    private double _target;
    private CancellationTokenSource? _animation;

    /// <summary>滚轮一格对应的像素数。</summary>
    public double WheelStep
    {
        get => GetValue(WheelStepProperty);
        set => SetValue(WheelStepProperty, value);
    }

    /// <summary>动画时长（毫秒）。</summary>
    public double AnimationDuration
    {
        get => GetValue(AnimationDurationProperty);
        set => SetValue(AnimationDurationProperty, value);
    }

    /// <summary>把相对位移换算为新的目标偏移（纯函数，便于测试）。</summary>
    public static double ComputeTarget(double current, double delta, double extent, double viewport)
    {
        double max = Math.Max(0d, extent - viewport);
        return Math.Clamp(current + delta, 0d, max);
    }

    /// <inheritdoc />
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        double scrollable = Extent.Height - Viewport.Height;
        if (e.Delta.Y == 0 || scrollable <= 0 || ShouldDeferToChild(e.Source))
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        e.Handled = true;
        double delta = -e.Delta.Y * WheelStep;
        _target = ComputeTarget(_target, delta, Extent.Height, Viewport.Height);
        AnimateToTarget();
    }

    /// <inheritdoc />
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);

        // 非动画期间的滚动（拖动滚动条、内容变化）同步目标值，避免下一次滚轮跳变。
        if (_animation is null)
        {
            _target = Offset.Y;
        }
    }


    private static bool ShouldDeferToChild(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        foreach (Visual ancestor in visual.GetSelfAndVisualAncestors())
        {
            if (ancestor is TextBox { AcceptsReturn: true })
            {
                return true;
            }

            if (ancestor is ComboBoxItem)
            {
                return true;
            }

            if (ancestor is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                return true;
            }
        }

        return false;
    }

    private void AnimateToTarget()
    {
        _animation?.Cancel();
        _animation?.Dispose();

        CancellationTokenSource cts = new();
        _animation = cts;

        double from = Offset.Y;
        double to = _target;
        if (Math.Abs(to - from) < 0.5)
        {
            return;
        }

        Animation animation = new()
        {
            Duration = TimeSpan.FromMilliseconds(Math.Max(60d, AnimationDuration)),
            Easing = new QuinticEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OffsetProperty, new Vector(Offset.X, from)) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OffsetProperty, new Vector(Offset.X, to)) } },
            },
        };

        _ = RunAsync(animation, cts);
    }

    private async Task RunAsync(Animation animation, CancellationTokenSource cts)
    {
        try
        {
            await animation.RunAsync(this, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // 被新的滚轮输入打断：正常控制流。
        }
        finally
        {
            if (ReferenceEquals(_animation, cts))
            {
                _animation = null;
            }

            cts.Dispose();
        }
    }
}