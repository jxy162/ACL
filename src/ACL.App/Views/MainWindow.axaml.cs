using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ACL.App.ViewModels;

namespace ACL.App.Views;

/// <summary>主窗口。</summary>
public partial class MainWindow : Window
{
    private Border? _rootFrame;

    /// <summary>构造主窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        ApplyPageTransition();
        Closing += (_, _) => (DataContext as MainWindowViewModel)?.SaveSettingsOnExit();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
            {
                Border? frame = _rootFrame ??= this.FindControl<Border>("RootFrame");
                if (frame is not null)
                {
                    frame.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(14);
                }
            }
        };
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.IsDialogVisible) && vm.IsDialogVisible)
                {
                    PlayDialogEntrance();
                }

                if (args.PropertyName == nameof(MainWindowViewModel.IsDialogVisible) && !vm.IsDialogVisible)
                {
                    ResetDialogVisuals();
                }
            };
        }
    }

    /// <summary>关闭后复位对话框视觉状态，供下次进场动画使用。</summary>
    private void ResetDialogVisuals()
    {
        DialogBackdrop.Opacity = 0d;
        DialogCard.Opacity = 0d;
        DialogCard.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translate(0px,22px) scale(0.96)");
    }

    /// <summary>装配页面切换过渡（按“减弱动效”设置）。</summary>
    private void ApplyPageTransition()
        => PageHost.PageTransition = ACL.App.Animations.ExpressivePageTransition.Create(
            ACL.App.Animations.ExpressivePageTransition.ReduceMotion);

    /// <summary>对话框进场：驱动属性到目标值，由样式中的 Transitions 负责动画。</summary>
    private void PlayDialogEntrance()
    {
        DialogBackdrop.Opacity = 1d;
        DialogCard.Opacity = 1d;
        DialogCard.RenderTransform = null;
    }
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
        else if (e.KeyModifiers == KeyModifiers.Control && e.Key is >= Key.D1 and <= Key.D7)
        {
            int index = e.Key - Key.D1;
            if (index < vm.Pages.Count)
            {
                vm.ShowPageCommand.Execute(vm.Pages[index]);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape && vm.IsDialogVisible)
        {
            vm.CancelLoginCommand.Execute(null);
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

    /// <summary>窗口级兜底：顶部条带内（且未被子控件处理）的按下同样触发拖拽 / 双击最大化。</summary>
    private void OnWindowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        double y = e.GetPosition(this).Y;
        if (y <= 58d)
        {
            OnTitleBarPressed(sender, e);
        }
    }

    private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // 双击标题栏：最大化 / 还原。
        if (e.ClickCount == 2)
        {
            OnToggleMaximize(this, new RoutedEventArgs());
            return;
        }

        BeginMoveDrag(e);

        // 贴边吸附：拖动结束后贴近屏幕顶部 → 最大化。
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is not null && WindowState == WindowState.Normal && Position.Y <= screen.WorkingArea.Y + 6)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnMinimize(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnToggleMaximize(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}