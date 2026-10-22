using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>启动页左栏（可交互）：账号 + 版本 + Java。</summary>
public partial class AccountLeftPanelViewModel : ObservableObject
{
    /// <summary>构造启动页左栏。</summary>
    public AccountLeftPanelViewModel(AppState state)
    {
        State = state;
    }

    /// <summary>共享状态。</summary>
    public AppState State { get; }
}