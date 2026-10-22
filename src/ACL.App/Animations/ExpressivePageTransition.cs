using Avalonia;
using Avalonia.Animation;

namespace ACL.App.Animations;

/// <summary>
/// 页面切换过渡：进入页淡入 + 竖向位移、离开页淡出。
/// 使用框架内置过渡（自动匹配动画器），并以本项目缓动族控制手感；开启“减弱动效”时退化为短淡入。
/// </summary>
public sealed class ExpressivePageTransition : IPageTransition
{
    /// <summary>是否减弱动效（由设置驱动）。</summary>
    public static bool ReduceMotion { get; set; }

    /// <summary>创建过渡实例（按设置选择）。</summary>
    public static IPageTransition Create(bool reduceMotion)
        => reduceMotion
            ? new CrossFade { Duration = TimeSpan.FromMilliseconds(130) }
            : new CompositePageTransition
            {
                PageTransitions =
                {
                    new CrossFade
                    {
                        Duration = TimeSpan.FromMilliseconds(240),
                    },
                    new PageSlide
                    {
                        Duration = TimeSpan.FromMilliseconds(320),
                        Orientation = PageSlide.SlideAxis.Vertical,
                        SlideInEasing = new AclEaseOutSmooth(AclEasePower.Strong),
                        SlideOutEasing = new AclEaseInSmooth(AclEasePower.Weak),
                    },
                },
            };

    /// <inheritdoc />
    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        => Create(ReduceMotion).Start(from, to, forward, cancellationToken);
}