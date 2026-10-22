using ACL.App.ViewModels;
using Avalonia.Controls;
using ACL.App.Views;
using Avalonia.Headless;
using Xunit;

namespace ACL.App.Tests;

public class MainWindowTests
{
    [Fact]
    public void DefaultPage_IsLaunch()
    {
        MainWindowViewModel vm = new();

        Assert.Equal("启动", vm.CurrentPage.Title);
        Assert.True(vm.Pages[0].IsSelected);
        Assert.All(vm.Pages, page => Assert.NotNull(page.Select));
    }

    [Fact]
    public void SelectingPage_UpdatesSelectionAndContent()
    {
        MainWindowViewModel vm = new();
        NavItem download = vm.Pages.Single(page => page.Key == "download");

        download.Select!.Execute(download);

        Assert.Equal("下载", vm.CurrentPage.Title);
        Assert.True(download.IsSelected);
        Assert.False(vm.Pages[0].IsSelected);
    }

    [Fact]
    public void Pages_ContainExpectedKeys()
    {
        MainWindowViewModel vm = new();

        Assert.Equal(["launch", "games", "download", "resources", "dashboard", "settings", "more"], vm.Pages.Select(page => page.Key));
    }

    [Fact]
    public void CancelLogin_OnIdleViewModel_DoesNotThrow()
    {
        // 回归：取消登录曾因异常逃逸（AsyncRelayCommand 重新抛出）导致无响应后崩溃。
        MainWindowViewModel vm = new();

        vm.CancelLoginCommand.Execute(null);

        Assert.False(vm.IsDialogVisible);
    }

    [Fact]
    public async Task DialogEntrance_DoesNotThrow()
    {
        // 回归：对话框进场动画曾因缺少 RenderTransform 的 animator 直接崩溃。
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindowViewModel vm = new();
                MainWindow window = new() { DataContext = vm };
                window.Show();

                vm.DialogTitle = "登录 Minecraft";
                vm.DialogCode = "ABCD-EFGH";
                vm.DialogMessage = "测试";
                vm.IsDialogVisible = true;

                Assert.True(vm.IsDialogVisible);

                vm.IsDialogVisible = false;
                Assert.False(vm.IsDialogVisible);
            },
            CancellationToken.None);
    }

    [Fact]
    public void SmoothScroll_TargetIsClamped()
    {
        // 平滑滚动：目标偏移按可滚动范围钳制（纯函数，便于回归）。
        Assert.Equal(0d, ACL.App.Controls.SmoothScrollViewer.ComputeTarget(0, -500, 2000, 600));
        Assert.Equal(1400d, ACL.App.Controls.SmoothScrollViewer.ComputeTarget(1400, 500, 2000, 600));
        Assert.Equal(200d, ACL.App.Controls.SmoothScrollViewer.ComputeTarget(0, 200, 2000, 600));
        Assert.Equal(0d, ACL.App.Controls.SmoothScrollViewer.ComputeTarget(100, 100, 500, 600));
    }

    [Fact]
    public async Task TitleBarStrip_IsHitTestable_ForDragging()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindow window = new() { DataContext = new MainWindowViewModel() };
                window.Show();

                // 回归：标题栏条带内的空白处必须可命中（否则拖拽区只剩 LOGO/文字，触发区域过小）。
                Avalonia.Controls.Grid? titleBar = window.FindControl<Avalonia.Controls.Grid>("TitleBar");
                Assert.NotNull(titleBar);
                Assert.True(titleBar!.Bounds.Width > 400, $"标题栏尺寸异常：{titleBar.Bounds}");

                Assert.NotNull(titleBar!.Background);
                Assert.True(titleBar.Bounds.Height >= 52, $"标题栏高度过小：{titleBar.Bounds.Height}");
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task Window_SupportsMaximizeMinimizeRestore()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindow window = new() { DataContext = new MainWindowViewModel() };
                window.Show();

                // 回归：最小化 / 最大化 / 还原 不得抛异常（曾因 x:Name 字段为空而崩溃）。
                window.WindowState = WindowState.Minimized;
                Assert.Equal(WindowState.Minimized, window.WindowState);

                window.WindowState = WindowState.Maximized;
                Assert.Equal(WindowState.Maximized, window.WindowState);

                window.WindowState = WindowState.Normal;
                Assert.Equal(WindowState.Normal, window.WindowState);
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task Window_ConstructsAndShows()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindow window = new() { DataContext = new MainWindowViewModel() };
                window.Show();
                Assert.True(window.IsVisible);
                Assert.Equal("ACL", window.Title);
            },
            CancellationToken.None);
    }
}