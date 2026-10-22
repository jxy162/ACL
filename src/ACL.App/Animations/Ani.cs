using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace ACL.App.Animations;

/// <summary>一条动画指令。</summary>
public sealed record AniSpec(string Name, Func<CancellationToken, Task> Run);

/// <summary>
/// 轻量动画引擎（对齐参考实现的 AniStart / Aa* 模型）：
/// 同名动画组会被打断并从当前值重新出发；只动 double 属性（透明度与平移/缩放分量），
/// 因此无需注册动画器，且天然可被打断、无跳变。
/// </summary>
public static class Ani
{
    private static readonly Dictionary<string, CancellationTokenSource> Groups = new(StringComparer.Ordinal);

    /// <summary>目标元素上的平移与缩放（首次使用时挂载）。</summary>
    public sealed class Motion
    {
        public TranslateTransform Translate { get; } = new();

        public ScaleTransform Scale { get; } = new();
    }

    private static readonly Dictionary<Visual, Motion> Motions = [];

    /// <summary>透明度动画。</summary>
    public static AniSpec Opacity(Visual visual, double to, int milliseconds = 400, int delay = 0, Easing? ease = null)
        => new($"{visual.GetHashCode()}.Opacity", ct => RunDoubleAsync(visual, Visual.OpacityProperty, to, milliseconds, delay, ease ?? new AclEaseOutSmooth(), ct));

    // 注意：不要对 TranslateTransform/ScaleTransform 的分量做动画——Avalonia 会交给 TransformAnimator，
    // 而它要求目标为 Visual，会抛 InvalidCastException（已在运行时复现）。
    // 位移与缩放请使用声明式 Transitions（TransformOperationsTransition）或内置 PageSlide。
    /// <summary>任意 double 属性动画。</summary>
    public static AniSpec Double(Animatable target, StyledProperty<double> property, double to, int milliseconds = 400, int delay = 0, Easing? ease = null)
        => new($"{target.GetHashCode()}.{property.Name}", ct => RunDoubleAsync(target, property, to, milliseconds, delay, ease ?? new AclEaseOutSmooth(), ct));

    /// <summary>延时代码。</summary>
    public static AniSpec Code(Action action, int delay = 0)
        => new($"Code.{action.GetHashCode()}", async ct =>
        {
            if (delay > 0)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }

            action();
        });

    /// <summary>播放一组动画并等待完成（同名组会先被打断）。</summary>
    public static async Task PlayAsync(string group, params AniSpec[] specs)
    {
        CancelInternal(group);
        CancellationTokenSource cts = new();
        Groups[group] = cts;
        try
        {
            await Task.WhenAll(specs.Select(spec => spec.Run(cts.Token))).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 被同名动画组打断：正常控制流。
        }
        finally
        {
            if (Groups.TryGetValue(group, out CancellationTokenSource? current) && ReferenceEquals(current, cts))
            {
                Groups.Remove(group);
            }

            cts.Dispose();
        }
    }

    /// <summary>立即打断同名动画组。</summary>
    public static void Cancel(string group) => CancelInternal(group);

    /// <summary>把当前平移/缩放写回元素（外部需要读取现状时使用）。</summary>
    public static (double X, double Y, double Scale) Current(Visual visual)
    {
        Motion motion = EnsureMotion(visual);
        return (motion.Translate.X, motion.Translate.Y, motion.Scale.ScaleX);
    }

    /// <summary>确保元素已挂载平移与缩放变换。</summary>
    public static Motion EnsureMotion(Visual visual)
    {
        ArgumentNullException.ThrowIfNull(visual);
        if (Motions.TryGetValue(visual, out Motion? existing))
        {
            return existing;
        }

        Motion motion = new();
        TransformGroup group = new() { Children = { motion.Translate, motion.Scale } };
        visual.RenderTransform = group;
        Motions[visual] = motion;
        return motion;
    }

    private static void CancelInternal(string group)
    {
        if (Groups.TryGetValue(group, out CancellationTokenSource? cts))
        {
            Groups.Remove(group);
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // 已结束。
            }

            cts.Dispose();
        }
    }

    private static async Task RunDoubleAsync(
        Animatable target,
        StyledProperty<double> property,
        double to,
        int milliseconds,
        int delay,
        Easing ease,
        CancellationToken cancellationToken)
    {
        double from = target.GetValue(property);
        if (Math.Abs(to - from) < 0.0005)
        {
            target.SetValue(property, to);
            return;
        }

        Animation animation = new()
        {
            Duration = TimeSpan.FromMilliseconds(Math.Max(1, milliseconds)),
            Delay = delay > 0 ? TimeSpan.FromMilliseconds(delay) : TimeSpan.Zero,
            Easing = ease,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(property, from) } },
                new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(property, to) } },
            },
        };

        await animation.RunAsync(target, cancellationToken).ConfigureAwait(false);
    }
}