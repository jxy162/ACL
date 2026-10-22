using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>左栏中的一条“标签 / 值”。</summary>
public partial class LeftPanelField : ObservableObject
{
    /// <summary>构造字段。</summary>
    public LeftPanelField(string label, string value)
    {
        Label = label;
        _value = value;
    }

    /// <summary>标签。</summary>
    public string Label { get; }

    /// <summary>值（可实时更新）。</summary>
    [ObservableProperty]
    private string _value;
}

/// <summary>左栏中的一个操作按钮。</summary>
/// <param name="Label">按钮文字。</param>
/// <param name="Command">命令。</param>
/// <param name="Parameter">命令参数。</param>
public sealed record LeftPanelAction(string Label, ICommand Command, object? Parameter = null);

/// <summary>
/// 页面左栏（描述式）：标题 + 说明 + 页面专属字段与操作。
/// 每个页面给出不同的内容，因此切换页面时左栏是"重新设计"的，而不是同一份侧栏。
/// </summary>
public partial class LeftPanelViewModel : ObservableObject
{
    /// <summary>构造左栏。</summary>
    public LeftPanelViewModel(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }

    /// <summary>标题。</summary>
    public string Title { get; }

    /// <summary>说明。</summary>
    public string Subtitle { get; }

    /// <summary>字段列表。</summary>
    public ObservableCollection<LeftPanelField> Fields { get; } = [];

    /// <summary>操作按钮。</summary>
    public ObservableCollection<LeftPanelAction> Actions { get; } = [];

    /// <summary>底部提示（可空）。</summary>
    [ObservableProperty]
    private string _hint = string.Empty;

    /// <summary>新增或更新字段并返回自身（便于链式构建）。</summary>
    public LeftPanelViewModel Set(string label, string value)
    {
        LeftPanelField? existing = Fields.FirstOrDefault(f => f.Label == label);
        if (existing is null)
        {
            Fields.Add(new LeftPanelField(label, value));
        }
        else
        {
            existing.Value = value;
        }

        return this;
    }

    /// <summary>追加操作按钮。</summary>
    public LeftPanelViewModel Add(string label, ICommand command, object? parameter = null)
    {
        Actions.Add(new LeftPanelAction(label, command, parameter));
        return this;
    }
}