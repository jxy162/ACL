using System.Windows.Input;
using ACL.Core.Domain;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ACL.App.ViewModels;

/// <summary>资源搜索结果行。</summary>
public partial class ResourceRowViewModel : ObservableObject
{
    /// <summary>构造行。</summary>
    public ResourceRowViewModel(ResourceProject project) => Project = project;

    /// <summary>项目。</summary>
    public ResourceProject Project { get; }

    /// <summary>标题。</summary>
    public string Title => Project.Title;

    /// <summary>简介。</summary>
    public string Description => Project.Description ?? string.Empty;

    /// <summary>作者。</summary>
    public string Author => Project.Author ?? "未知作者";

    /// <summary>下载量文本。</summary>
    public string Downloads => Project.Downloads >= 1000
        ? $"{Project.Downloads / 1000.0:F1}k"
        : Project.Downloads.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>安装状态。</summary>
    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>安装命令（由页面注入）。</summary>
    public ICommand? Install { get; set; }
}