using ACL.App.ViewModels;
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

        Assert.Equal(["launch", "games", "download", "resources", "settings", "more"], vm.Pages.Select(page => page.Key));
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