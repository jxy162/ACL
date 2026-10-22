using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ACL.App.ViewModels;
using ACL.App.Views;

namespace ACL.App;

/// <summary>ACL 界面应用。</summary>
public partial class App : Avalonia.Application
{
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