using System.Collections.ObjectModel;
using ACL.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>游戏列表页：本机已安装与可选版本，含游玩时长与快捷启动。</summary>
public partial class GameListPageViewModel : PageViewModelBase
{

    private readonly AppServices _services;
    private readonly AppState _state;
    private readonly Action<VersionRowViewModel> _startVersion;

    /// <summary>构造游戏列表页。</summary>
    public GameListPageViewModel(AppServices services, AppState state, Action<VersionRowViewModel> startVersion)
        : base("游戏列表", "本机版本一览：安装状态、游玩时长与快捷启动。")
    {
        _services = services;
        _state = state;
        _startVersion = startVersion;

        _state.PlaytimeChanged += (_, _) => ApplyFilter();
    }

    /// <summary>筛选后的列表。</summary>
    public ObservableCollection<VersionRowViewModel> Rows { get; } = [];

    /// <summary>仅显示已安装。</summary>
    [ObservableProperty]
    private bool _installedOnly;

    /// <summary>状态。</summary>
    [ObservableProperty]
    private string _status = "在“下载”页刷新版本清单后，这里会列出所有版本。";

    partial void OnInstalledOnlyChanged(bool value) => ApplyFilter();

    /// <summary>重建列表（含安装检测与游玩时长）。</summary>
    public void ApplyFilter()
    {
        Rows.Clear();
        int installed = 0;
        foreach (VersionRowViewModel row in _state.Versions)
        {
            row.Start = StartCommand;
            row.IsInstalled = _services.FileSystem.FileExists(
                Path.Combine(_services.GameDirectory, "versions", row.Id, row.Id + ".jar"));
            row.PlayMinutes = _state.GetPlayMinutes(row.Id);

            if (row.IsInstalled)
            {
                installed++;
            }

            if (InstalledOnly && !row.IsInstalled)
            {
                continue;
            }

            Rows.Add(row);
        }

        Status = _state.Versions.Count == 0
            ? "尚未获取版本清单，请到“下载”页点击“刷新版本清单”。"
            : $"共 {_state.Versions.Count} 个版本，其中已安装 {installed} 个；显示 {Rows.Count} 个。";
    }

    /// <summary>快捷启动：选中该版本并回到“启动”页生成计划。</summary>
    [RelayCommand]
    private void Start(VersionRowViewModel? row)
    {
        if (row is not null)
        {
            _startVersion(row);
        }
    }
}