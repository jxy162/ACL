namespace ACL.Core.Abstractions;

/// <summary>
/// 带值的结果载体。预期失败用 <see cref="ErrorCode"/> 表达；
/// <see cref="Detail"/> 仅用于日志与诊断，不应直接展示给用户。
/// </summary>
/// <typeparam name="T">成功时的值类型。</typeparam>
public readonly record struct Result<T>(T? Value, ErrorCode Code, string? Detail = null)
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess => Code == ErrorCode.None;

    /// <summary>构造成功结果。</summary>
    public static Result<T> Success(T value) => new(value, ErrorCode.None);

    /// <summary>构造失败结果。</summary>
    public static Result<T> Failure(ErrorCode code, string? detail = null) => new(default, code, detail);

    /// <summary>成功时返回值，失败时抛出 <see cref="InvalidOperationException"/>。</summary>
    public T ValueOrThrow() => IsSuccess
        ? Value!
        : throw new InvalidOperationException($"结果处于失败态：{Code}。");

    /// <summary>尝试取值。</summary>
    public bool TryGet(out T value)
    {
        value = Value!;
        return IsSuccess;
    }

    /// <summary>将成功值映射为新类型；失败态原样传递。</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return IsSuccess ? Result<TOut>.Success(selector(Value!)) : Result<TOut>.Failure(Code, Detail);
    }
}

/// <summary>无返回值的结果载体。</summary>
public readonly record struct Result(ErrorCode Code, string? Detail = null)
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess => Code == ErrorCode.None;

    /// <summary>成功。</summary>
    public static Result Ok => new(ErrorCode.None);

    /// <summary>失败。</summary>
    public static Result Failure(ErrorCode code, string? detail = null) => new(code, detail);
}