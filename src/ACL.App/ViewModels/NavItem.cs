using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>底部导航项。</summary>
public partial class NavItem : ObservableObject
{
    /// <summary>构造导航项。</summary>
    private readonly string _iconData;
    private Geometry? _icon;

    public NavItem(string key, string iconData, PageViewModelBase page)
    {
        Key = key;
        _iconData = iconData;
        Page = page;
    }

    /// <summary>图标几何（延迟解析：仅在界面线程首次读取时创建）。</summary>
    public Geometry Icon => _icon ??= Geometry.Parse(_iconData);

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