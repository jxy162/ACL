using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>底部导航项。</summary>
public partial class NavItem : ObservableObject
{
    /// <summary>构造导航项。</summary>
    public NavItem(string key, PageViewModelBase page)
    {
        Key = key;
        Page = page;
    }

    /// <summary>标识。</summary>
    public string Key { get; }

    /// <summary>关联页面。</summary>
    public PageViewModelBase Page { get; }

    /// <summary>显示名（取页面标题）。</summary>
    public string Label => Page.Title;

    /// <summary>是否选中。</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>选择命令（由视图模型注入）。</summary>
    public ICommand? Select { get; set; }
}