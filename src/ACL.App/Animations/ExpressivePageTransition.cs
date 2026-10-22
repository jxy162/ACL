using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace ACL.App.Animations;

/// <summary>
/// M3 Expressive 风格的页面切换：淡入 + 轻微上移与缩放（正向），反向时镜像。
/// 属性由当前可见值重新出发，因此可被打断并重定向（不产生跳变）。
/// </summary>
public sealed class ExpressivePageTransition : IPageTransition
{
    /// <summary>是否减弱动效（跟随系统 / 用户设置）。</summary>
    public static bool ReduceMotion { get; set; }

    /// <summary>动画时长。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <inheritdoc />
    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        bool reduce = ReduceMotion;
        List<Task> running = [];

        if (to is not null)
        {
            to.IsVisible = true;
            running.Add(AnimateAsync(
                to,
                fromOpacity: 0d,
                toOpacity: 1d,
                fromTransform: reduce ? "none" : $"translate(0px,{(forward ? 18 : -18)}px) scale(0.985)",
                toTransform: "none"));
        }

        if (from is not null)
        {
            running.Add(AnimateAsync(
                from,
                fromOpacity: 1d,
                toOpacity: 0d,
                fromTransform: "none",
                toTransform: reduce ? "none" : $"translate(0px,{(forward ? -12 : 12)}px) scale(0.99)",
                hideAtEnd: true));
        }

        if (running.Count > 0)
        {
            await Task.WhenAll(running).WaitAsync(cancellationToken);
        }
    }

    private async Task AnimateAsync(
        Visual target,
        double fromOpacity,
        double toOpacity,
        string fromTransform,
        string toTransform,
        bool hideAtEnd = false)
    {
        Animation animation = new()
        {
            Duration = ReduceMotion ? TimeSpan.FromMilliseconds(140) : Duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, fromOpacity),
                        new Setter(Visual.RenderTransformProperty, TransformOperations.Parse(fromTransform)),
                    },
                },
                new KeyFrame
                {
                    Cue = new Cue(1d),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, toOpacity),
                        new Setter(Visual.RenderTransformProperty, TransformOperations.Parse(toTransform)),
                    },
                },
            },
        };

        await animation.RunAsync(target);

        if (hideAtEnd)
        {
            target.IsVisible = false;
            target.Opacity = 1d;
            target.RenderTransform = null;
        }
    }
}