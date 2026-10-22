using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>主窗口视图模型：导航与当前页面。</summary>
public partial class MainWindowViewModel : ObservableObject
{
    /// <summary>构造视图模型。</summary>
    public MainWindowViewModel()
    {
        SelectPageCommand = new RelayCommand<NavItem>(SelectPage);

        Pages =
        [
            new NavItem("launch", "启动", "选择版本与账号，一键开始游戏。", "内核：Version / Java / Auth / Launch"),
            new NavItem("download", "下载", "安装原版、加载器与整合包。", "内核：VersionCatalog / DownloadService / Modpack"),
            new NavItem("settings", "设置", "内存、镜像源、Java 路径与账号。", "内核：ConfigurationStore（M9）"),
            new NavItem("more", "更多", "日志、诊断与关于。", "内核：Diagnostics（M9）"),
        ];

        foreach (NavItem page in Pages)
        {
            page.Select = SelectPageCommand;
        }

        _currentPage = ToPage(Pages[0]);
        Pages[0].IsSelected = true;
    }

    /// <summary>导航项。</summary>
    public ObservableCollection<NavItem> Pages { get; }

    /// <summary>选择页面的命令。</summary>
    public ICommand SelectPageCommand { get; }

    /// <summary>当前页面。</summary>
    [ObservableProperty]
    private PageViewModel _currentPage;

    /// <summary>窗口标题。</summary>
    public string Title => "ACL";

    /// <summary>副标题。</summary>
    public string Subtitle => "Minecraft 启动器";

    private void SelectPage(NavItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (NavItem page in Pages)
        {
            page.IsSelected = ReferenceEquals(page, item);
        }

        CurrentPage = ToPage(item);
    }

    private static PageViewModel ToPage(NavItem item) => new(item.Label, item.Description, item.Hint);
}