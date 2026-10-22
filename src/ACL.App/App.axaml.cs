using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using ACL.App.ViewModels;
using ACL.App.Views;

namespace ACL.App;

/// <summary>ACL 界面应用。</summary>
public partial class App : Avalonia.Application
{
    private const string DarkThemePath = "avares://ACL.App/Themes/Dark.axaml";
    private const string LightThemePath = "avares://ACL.App/Themes/Light.axaml";

    /// <summary>切换主题（<c>dark</c> / <c>light</c>）。</summary>
    public static void ApplyTheme(string theme)
    {
        if (Current is not App app)
        {
            return;
        }

        bool light = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        Uri themeUri = new(light ? LightThemePath : DarkThemePath);
        ResourceInclude include = new(themeUri) { Source = themeUri };
        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(include);
        app.RequestedThemeVariant = light ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowViewModel viewModel = new();
            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // 启动即拉取版本清单，避免下拉框为空。
            viewModel.RefreshVersionsCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}