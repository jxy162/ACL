using Avalonia;
using Avalonia.Headless;
using ACL.App;

[assembly: AvaloniaTestApplication(typeof(ACL.App.Tests.TestAppBuilder))]

namespace ACL.App.Tests;

/// <summary>无头测试用的应用构建器。</summary>
public static class TestAppBuilder
{
    /// <summary>构建无头 Avalonia 应用。</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}