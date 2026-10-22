using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ACL.App.ViewModels;

namespace ACL.App.Views;

/// <summary>主窗口。</summary>
public partial class MainWindow : Window
{
    /// <summary>构造主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        Closing += (_, _) => (DataContext as MainWindowViewModel)?.SaveSettingsOnExit();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (e.Key == Key.F11)
        {
            vm.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key is >= Key.D1 and <= Key.D5)
        {
            int index = e.Key - Key.D1;
            if (index < vm.Pages.Count)
            {
                vm.ShowPageCommand.Execute(vm.Pages[index]);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape && vm.IsFullscreen)
        {
            vm.ToggleFullscreenCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}