using Avalonia.Animation;

namespace ACL.App.Animations;

/// <summary>
/// 页面切换过渡工厂：常规为「淡入 + 竖向位移」组合过渡；开启减弱动效时退化为短淡入。
/// 使用框架内置过渡（会自动匹配动画器），避免手动动画在 Avalonia 12 上因内部动画器不可用而失败。
/// </summary>
public static class ExpressivePageTransition
{
    /// <summary>是否减弱动效（由设置驱动）。</summary>
    public static bool ReduceMotion { get; set; }

    /// <summary>创建过渡实例。</summary>
    public static IPageTransition Create(bool reduceMotion)
        => reduceMotion ? CreateReduced() : CreateExpressive();

    /// <summary>常规过渡：淡入 + 竖向位移。</summary>
    public static IPageTransition CreateExpressive() => new CompositePageTransition
    {
        PageTransitions =
        {
            new CrossFade { Duration = TimeSpan.FromMilliseconds(240) },
            new PageSlide
            {
                Duration = TimeSpan.FromMilliseconds(300),
                Orientation = PageSlide.SlideAxis.Vertical,
            },
        },
    };

    /// <summary>减弱动效：仅短淡入。</summary>
    public static IPageTransition CreateReduced()
        => new CrossFade { Duration = TimeSpan.FromMilliseconds(120) };
}