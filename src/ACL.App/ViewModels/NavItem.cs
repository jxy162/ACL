using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>底部导航项。</summary>
public partial class NavItem : ObservableObject
{
    /// <summary>构造导航项。</summary>
    public NavItem(string key, string label, string description, string hint)
    {
        Key = key;
        Label = label;
        Description = description;
        Hint = hint;
    }

    /// <summary>标识。</summary>
    public string Key { get; }

    /// <summary>显示名。</summary>
    public string Label { get; }

    /// <summary>页面描述。</summary>
    public string Description { get; }

    /// <summary>页面提示。</summary>
    public string Hint { get; }

    /// <summary>是否选中。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>选择命令（由视图模型注入）。</summary>
    public ICommand? Select { get; set; }
}