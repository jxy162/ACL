namespace ACL.App.ViewModels;

/// <summary>当前页面的展示模型。</summary>
/// <param name="Title">标题。</param>
/// <param name="Description">说明。</param>
/// <param name="Hint">提示。</param>
public sealed record PageViewModel(string Title, string Description, string Hint);