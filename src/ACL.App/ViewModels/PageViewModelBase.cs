using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>页面视图模型基类。</summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>构造页面。</summary>
    protected PageViewModelBase(string title, string description)
    {
        Title = title;
        Description = description;
    }

    /// <summary>页面标题。</summary>
    public string Title { get; }

    /// <summary>页面说明。</summary>
    public string Description { get; }

    /// <summary>是否忙。</summary>
    [ObservableProperty]
    private bool _isBusy;
}