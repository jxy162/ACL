using Avalonia.Animation.Easings;

namespace ACL.App.Animations;

/// <summary>缓动强度（对齐参考实现的 Weak / Middle / Strong / ExtraStrong）。</summary>
public enum AclEasePower
{
    /// <summary>弱。</summary>
    Weak = 2,

    /// <summary>中。</summary>
    Middle = 3,

    /// <summary>强。</summary>
    Strong = 4,

    /// <summary>极强。</summary>
    ExtraStrong = 5,
}

/// <summary>平滑起步：t^p。</summary>
public sealed class AclEaseInSmooth(AclEasePower power = AclEasePower.Middle) : Easing
{
    private readonly int _p = (int)power;

    /// <inheritdoc />
    public override double Ease(double progress) => Math.Pow(Math.Clamp(progress, 0d, 1d), _p);
}

/// <summary>平滑收尾：1-(1-t)^p（参考实现中最常用的缓动）。</summary>
public sealed class AclEaseOutSmooth(AclEasePower power = AclEasePower.Middle) : Easing
{
    private readonly int _p = (int)power;

    /// <inheritdoc />
    public override double Ease(double progress) => 1d - Math.Pow(1d - Math.Clamp(progress, 0d, 1d), _p);
}

/// <summary>平滑起步与收尾（在中点拼接）。</summary>
public sealed class AclEaseInOutSmooth(AclEasePower power = AclEasePower.Middle, double middle = 0.5) : Easing
{
    private readonly AclEaseInSmooth _in = new(power);
    private readonly AclEaseOutSmooth _out = new(power);
    private readonly double _middle = middle;

    /// <inheritdoc />
    public override double Ease(double progress)
    {
        double t = Math.Clamp(progress, 0d, 1d);
        return t < _middle
            ? _middle * _in.Ease(t / _middle)
            : ((1d - _middle) * _out.Ease((t - _middle) / (1d - _middle))) + _middle;
    }
}

/// <summary>带回弹的收尾（有效时间约在后 1/3）。</summary>
public sealed class AclEaseOutBack(AclEasePower power = AclEasePower.Middle) : Easing
{
    private readonly double _p = 3d - ((int)power * 0.5d);

    /// <inheritdoc />
    public override double Ease(double progress)
    {
        double t = Math.Clamp(progress, 0d, 1d);
        return 1d - (Math.Pow(1d - t, _p) * Math.Cos(1.5d * Math.PI * t));
    }
}

/// <summary>带回弹的起步。</summary>
public sealed class AclEaseInBack(AclEasePower power = AclEasePower.Middle) : Easing
{
    private readonly double _p = 3d - ((int)power * 0.5d);

    /// <inheritdoc />
    public override double Ease(double progress)
    {
        double t = Math.Clamp(progress, 0d, 1d);
        return Math.Pow(t, _p) * Math.Cos(1.5d * Math.PI * (1d - t));
    }
}

/// <summary>弹簧收尾（约 40% 处到达最大值）。</summary>
public sealed class AclEaseOutElastic(AclEasePower power = AclEasePower.Middle) : Easing
{
    private readonly int _p = (int)power + 4;

    /// <inheritdoc />
    public override double Ease(double progress)
    {
        double t = 1d - Math.Clamp(progress, 0d, 1d);
        return 1d - (Math.Pow(t, (_p - 1) * 0.25d) * Math.Cos((_p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d)));
    }
}

/// <summary>起步回弹 + 平滑收尾（先冲后缓）。</summary>
public sealed class AclEaseInCar(AclEasePower power = AclEasePower.Middle, double middle = 0.7) : Easing
{
    private readonly AclEaseInBack _in = new(power);
    private readonly AclEaseOutSmooth _out = new(power);
    private readonly double _middle = middle;

    /// <inheritdoc />
    public override double Ease(double progress)
    {
        double t = Math.Clamp(progress, 0d, 1d);
        return t < _middle
            ? _middle * _in.Ease(t / _middle)
            : ((1d - _middle) * _out.Ease((t - _middle) / (1d - _middle))) + _middle;
    }
}