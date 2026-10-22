using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>版本分类（下载页 / 游戏列表按此分区显示）。</summary>
public static class VersionCategory
{
    /// <summary>正式版。</summary>
    public const string Release = "release";

    /// <summary>快照版。</summary>
    public const string Snapshot = "snapshot";

    /// <summary>预览版（rc / pre）。</summary>
    public const string Preview = "preview";

    /// <summary>远古版（Beta / Alpha）。</summary>
    public const string Ancient = "ancient";

    /// <summary>全部。</summary>
    public const string All = "all";

    /// <summary>分类展示名。</summary>
    public static string Label(string category) => category switch
    {
        Release => "正式版",
        Snapshot => "快照版",
        Preview => "预览版",
        Ancient => "远古版",
        _ => "全部版本",
    };

    /// <summary>由版本名与清单类型判断分类。</summary>
    public static string Of(string id, string type)
    {
        if (type is "old_beta" or "old_alpha")
        {
            return Ancient;
        }

        string lower = id.ToLowerInvariant();
        if (type == "snapshot")
        {
            return lower.Contains("pre", StringComparison.Ordinal) || lower.Contains("rc", StringComparison.Ordinal)
                ? Preview
                : Snapshot;
        }

        return Release;
    }
}

/// <summary>版本列表行。</summary>
public partial class VersionRowViewModel : ObservableObject
{
    /// <summary>构造版本行。</summary>
    public VersionRowViewModel(string id, string type, DateTimeOffset releaseTime)
    {
        Id = id;
        Type = type;
        ReleaseTime = releaseTime;
        Category = VersionCategory.Of(id, type);
    }

    /// <summary>版本名。</summary>
    public string Id { get; }

    /// <summary>清单类型（release / snapshot / old_beta / old_alpha）。</summary>
    public string Type { get; }

    /// <summary>发布时间。</summary>
    public DateTimeOffset ReleaseTime { get; }

    /// <summary>分类。</summary>
    public string Category { get; }

    /// <summary>发布时间文本。</summary>
    public string Released => ReleaseTime == default ? "—" : ReleaseTime.ToString("yyyy-MM-dd");

    /// <summary>本地是否已安装。</summary>
    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>累计游玩分钟数。</summary>
    [ObservableProperty]
    private double _playMinutes;

    /// <summary>安装时间（本机实例；未安装为默认值）。</summary>
    public DateTimeOffset InstalledTime { get; set; }

    /// <summary>加载器标签（本机实例的加载器；未安装为空）。</summary>
    public string LoaderLabel { get; set; } = string.Empty;

    /// <summary>已安装标记文本。</summary>
    public string InstalledLabel => IsInstalled ? "已安装" : string.Empty;

    /// <summary>游玩时长文本。</summary>
    public string PlayText => PlayMinutes <= 0
        ? "—"
        : PlayMinutes >= 60
            ? $"{PlayMinutes / 60:F1} h"
            : $"{PlayMinutes:F0} min";

    /// <summary>分类标签。</summary>
    public string CategoryLabel => VersionCategory.Label(Category);

    /// <summary>类型标签（兼容旧展示）。</summary>
    public string TypeLabel => CategoryLabel;

    /// <summary>安装命令（由页面注入）。</summary>
    public ICommand? Install { get; set; }

    /// <summary>启动命令（由游戏列表注入）。</summary>
    public ICommand? Start { get; set; }

    /// <summary>进入安装二级页面的命令（由下载页注入）。</summary>
    public ICommand? OpenInstall { get; set; }

    partial void OnIsInstalledChanged(bool value) => OnPropertyChanged(nameof(InstalledLabel));
    partial void OnPlayMinutesChanged(double value) => OnPropertyChanged(nameof(PlayText));
}