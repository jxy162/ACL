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