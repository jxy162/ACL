using System.Diagnostics;
using ACL.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ACL.App.ViewModels;

/// <summary>更多页。</summary>
public partial class MorePageViewModel : PageViewModelBase
{
    private readonly AppServices _services;

    /// <summary>构建更多页。</summary>
    public MorePageViewModel(AppServices services)
        : base("更多", "关于 ACL、数据目录与诊断入口。")
    {
        _services = services;
    }

    /// <summary>产品名。</summary>
    public string ProductName => "ACL";

    /// <summary>版本。</summary>
    public string AppVersion => "0.1.0（Kernel-β+）";

    /// <summary>内核说明。</summary>
    public string KernelNote => "完全自研内核：版本 / 下载 / 认证 / Java / 启动 / 资源 / 整合包";

    /// <summary>合规声明。</summary>
    public string Notice => "净室自研：仅依据公开规范与外部行为实现，不包含任何第三方启动器源码。";

    /// <summary>打开数据目录。</summary>
    [RelayCommand]
    private void OpenDataDirectory()
    {
        try
        {
            _services.FileSystem.CreateDirectory(_services.DataDirectory);
            Process.Start(new ProcessStartInfo { FileName = _services.DataDirectory, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // 忽略打开失败。
        }
    }
}