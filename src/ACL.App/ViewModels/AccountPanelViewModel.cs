using System.Collections.ObjectModel;
using System.Windows.Input;
using ACL.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>账号列表行（可高亮当前账号）。</summary>
public partial class AccountRowViewModel : ObservableObject
{
    /// <summary>构造账号行。</summary>
    public AccountRowViewModel(AccountProfile profile, ICommand activate, ICommand delete)
    {
        Profile = profile;
        ActivateCommand = activate;
        DeleteCommand = delete;
    }

    /// <summary>账号档案。</summary>
    public AccountProfile Profile { get; }

    /// <summary>激活命令。</summary>
    public ICommand ActivateCommand { get; }

    /// <summary>删除命令。</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>是否为当前账号。</summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>玩家名。</summary>
    public string Name => Profile.Name;

    /// <summary>类型标签。</summary>
    public string TypeLabel => Profile.TypeLabel;

    /// <summary>头像字符。</summary>
    public string AvatarLetter => Profile.AvatarLetter;
}

/// <summary>账号面板命令集合（由主窗口注入）。</summary>
/// <param name="CreateOffline">新建离线账号。</param>
/// <param name="LoginMicrosoft">正版登录（设备码）。</param>
/// <param name="Activate">切换账号。</param>
/// <param name="Delete">删除账号。</param>
public sealed record AccountPanelCommands(
    ICommand CreateOffline,
    ICommand LoginMicrosoft,
    ICommand Activate,
    ICommand Delete);

/// <summary>
/// 账号面板：离线登录与正版登录分开显示，并管理本机已登录的多账号。
/// </summary>
public partial class AccountPanelViewModel : ObservableObject
{
    private readonly AppState _state;

    /// <summary>构造账号面板。</summary>
    public AccountPanelViewModel(AppState state, AccountPanelCommands commands)
    {
        _state = state;
        CreateOfflineCommand = commands.CreateOffline;
        LoginMicrosoftCommand = commands.LoginMicrosoft;
        ActivateAccountCommand = commands.Activate;
        DeleteAccountCommand = commands.Delete;
        state.AccountsChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>共享状态。</summary>
    public AppState State => _state;

    /// <summary>离线账号（本机）。</summary>
    public ObservableCollection<AccountRowViewModel> OfflineAccounts { get; } = [];

    /// <summary>正版账号（本机）。</summary>
    public ObservableCollection<AccountRowViewModel> PremiumAccounts { get; } = [];

    /// <summary>新建离线账号用玩家名。</summary>
    [ObservableProperty]
    private string _newOfflineName = string.Empty;

    /// <summary>新建离线账号命令。</summary>
    public ICommand CreateOfflineCommand { get; }

    /// <summary>正版登录命令。</summary>
    public ICommand LoginMicrosoftCommand { get; }

    /// <summary>切换账号命令。</summary>
    public ICommand ActivateAccountCommand { get; }

    /// <summary>删除账号命令。</summary>
    public ICommand DeleteAccountCommand { get; }

    /// <summary>是否有正版账号。</summary>
    public bool HasPremium => PremiumAccounts.Count > 0;

    /// <summary>是否有离线账号。</summary>
    public bool HasOffline => OfflineAccounts.Count > 0;

    /// <summary>重建账号列表。</summary>
    public void Refresh()
    {
        OfflineAccounts.Clear();
        PremiumAccounts.Clear();
        foreach (AccountProfile profile in _state.Accounts)
        {
            bool current = _state.CurrentAccount is { } active && string.Equals(active.Id, profile.Id, StringComparison.Ordinal);
            AccountRowViewModel row = new(profile, ActivateAccountCommand, DeleteAccountCommand) { IsCurrent = current };
            if (profile.Type == AccountType.Offline)
            {
                OfflineAccounts.Add(row);
            }
            else
            {
                PremiumAccounts.Add(row);
            }
        }

        OnPropertyChanged(nameof(HasPremium));
        OnPropertyChanged(nameof(HasOffline));
    }
}