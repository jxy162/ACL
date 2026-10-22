namespace ACL.App.ViewModels;

/// <summary>页面与主窗口之间的协作接口（导航 / 启动 / 提示）。</summary>
public interface ILauncherHost
{
    /// <summary>启动指定实例（含进度覆盖层）。</summary>
    Task LaunchInstanceAsync(string instanceId);

    /// <summary>进入"安装"二级页面。</summary>
    void OpenInstallPage(VersionRowViewModel row);

    /// <summary>进入"实例设置"二级页面。</summary>
    void OpenInstanceSettings(InstalledInstanceRow row);

    /// <summary>按导航键切换页面。</summary>
    void ShowPage(string navKey);

    /// <summary>显示轻提示。</summary>
    void ShowToast(string text);

    /// <summary>重新扫描本机实例并刷新各页面。</summary>
    void RefreshInstances();
}