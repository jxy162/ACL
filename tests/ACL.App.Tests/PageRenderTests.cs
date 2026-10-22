using ACL.App.ViewModels;
using ACL.App.Views;
using ACL.Core.Domain;
using ACL.Core.Version;
using Avalonia.Controls;
using Avalonia.Headless;
using Xunit;

namespace ACL.App.Tests;

/// <summary>
/// 页面渲染回归：每个页面（含二级页面）都必须能在真实可视树中实例化其 DataTemplate，
/// 否则说明模板引用了不存在的类型 / 资源，属于发布前必须拦下的问题。
/// </summary>
public class PageRenderTests
{
    private static readonly string[] NavKeys = ["launch", "games", "download", "resources", "dashboard", "settings", "more"];

    [Fact]
    public async Task AllNavPages_RenderWithoutException()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindowViewModel vm = new();
                MainWindow window = new() { DataContext = vm };
                window.Show();

                foreach (string key in NavKeys)
                {
                    vm.ShowPage(key);
                    window.Measure(new Avalonia.Size(1000, 700));
                    window.Arrange(new Avalonia.Rect(0, 0, 1000, 700));
                    Assert.NotNull(vm.CurrentPage.LeftPanel);
                }
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task InstallAndInstanceSettingsSubPages_RenderWithoutException()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindowViewModel vm = new();
                MainWindow window = new() { DataContext = vm };
                window.Show();

                VersionRowViewModel row = new("1.20.1", "release", DateTimeOffset.UnixEpoch);
                vm.OpenInstallPage(row);
                window.Measure(new Avalonia.Size(1000, 700));
                window.Arrange(new Avalonia.Rect(0, 0, 1000, 700));
                Assert.IsType<InstallPageViewModel>(vm.CurrentPage);

                InstalledInstance instance = new(
                    "1.20.1",
                    "C:/game/versions/1.20.1",
                    "C:/game/versions/1.20.1/1.20.1.json",
                    true,
                    null,
                    ModLoaderKind.None,
                    null,
                    DateTimeOffset.UnixEpoch);
                InstalledInstanceRow instanceRow = new(instance, vm.LaunchPage.LaunchInstanceCommand, vm.LaunchPage.InstallSelectedCommand);
                vm.OpenInstanceSettings(instanceRow);
                window.Measure(new Avalonia.Size(1000, 700));
                window.Arrange(new Avalonia.Rect(0, 0, 1000, 700));
                Assert.IsType<InstanceSettingsPageViewModel>(vm.CurrentPage);
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task LaunchOverlay_TogglesWithoutException()
    {
        using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(
            () =>
            {
                MainWindowViewModel vm = new();
                MainWindow window = new() { DataContext = vm };
                window.Show();

                vm.State.IsLaunching = true;
                vm.State.LaunchStage = "补齐文件";
                vm.State.LaunchProgress = 0.42;
                window.Measure(new Avalonia.Size(1000, 700));
                window.Arrange(new Avalonia.Rect(0, 0, 1000, 700));

                Avalonia.Controls.Border? overlay = window.FindControl<Avalonia.Controls.Border>("LaunchOverlay");
                Assert.NotNull(overlay);
                Assert.True(overlay!.IsVisible);

                vm.HideLaunchCommand.Execute(null);
                Assert.False(vm.State.IsLaunching);
            },
            CancellationToken.None);
    }

    [Fact]
    public void AccountPanel_SeparatesOfflineAndPremium()
    {
        MainWindowViewModel vm = new();
        AppState state = vm.State;

        state.Accounts.Add(AccountProfile.Offline("Steve", DateTimeOffset.UnixEpoch));
        state.Accounts.Add(new AccountProfile("uuid-1", AccountType.Microsoft, "Alex", "uuid-1", DateTimeOffset.UnixEpoch));
        state.RaiseAccountsChanged();

        Assert.Single(vm.AccountPanel.OfflineAccounts);
        Assert.Single(vm.AccountPanel.PremiumAccounts);
        Assert.Equal("Steve", vm.AccountPanel.OfflineAccounts[0].Name);
        Assert.Equal("Alex", vm.AccountPanel.PremiumAccounts[0].Name);
    }

    [Fact]
    public void VersionCategories_SplitByTypeAndName()
    {
        Assert.Equal(VersionCategory.Release, VersionCategory.Of("1.20.1", "release"));
        Assert.Equal(VersionCategory.Snapshot, VersionCategory.Of("26.1-snapshot-1", "snapshot"));
        Assert.Equal(VersionCategory.Preview, VersionCategory.Of("1.21-pre1", "snapshot"));
        Assert.Equal(VersionCategory.Preview, VersionCategory.Of("1.21-rc1", "snapshot"));
        Assert.Equal(VersionCategory.Ancient, VersionCategory.Of("b1.7.3", "old_beta"));
        Assert.Equal(VersionCategory.Ancient, VersionCategory.Of("a1.2.6", "old_alpha"));
    }
}