using System.Windows.Input;

namespace ACL.App.ViewModels;

/// <summary>版本列表行。</summary>
public sealed class VersionRowViewModel
{
    /// <summary>构造版本行。</summary>
    public VersionRowViewModel(string id, string type, DateTimeOffset releaseTime)
    {
        Id = id;
        Type = type;
        ReleaseTime = releaseTime;
    }

    /// <summary>版本名。</summary>
    public string Id { get; }

    /// <summary>类型。</summary>
    public string Type { get; }

    /// <summary>发布时间。</summary>
    public DateTimeOffset ReleaseTime { get; }

    /// <summary>发布时间文本。</summary>
    public string Released => ReleaseTime == default ? "—" : ReleaseTime.ToString("yyyy-MM-dd");

    /// <summary>本地是否已存在该版本（客户端 jar）。</summary>
    public bool IsInstalled { get; set; }

    /// <summary>已安装标记文本。</summary>
    public string InstalledLabel => IsInstalled ? "已安装" : string.Empty;

    /// <summary>安装命令（由页面注入）。</summary>

    public ICommand? Install { get; set; }

    /// <summary>类型标签。</summary>
    public string TypeLabel => Type switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "旧 Beta",
        "old_alpha" => "旧 Alpha",
        _ => Type,
    };
}