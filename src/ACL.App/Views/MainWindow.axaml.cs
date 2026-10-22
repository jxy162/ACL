using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                RootFrame.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(14);
            }
        };
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
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key is >= Key.D1 and <= Key.D6)
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

    private static void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.DataTransfer?.Contains(DataFormat.File) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        foreach (IStorageItem item in e.DataTransfer?.TryGetFiles() ?? [])
        {
            string path = item.Path.LocalPath;
            if (path.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                await vm.ImportModpackAsync(path);
                break;
            }
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

    private void OnToggleMaximize(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}