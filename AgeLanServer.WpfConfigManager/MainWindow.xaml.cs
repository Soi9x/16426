using System.Windows;
using System.Windows.Input;
using AgeLanServer.WpfConfigManager.Models;
using AgeLanServer.WpfConfigManager.ViewModels;

namespace AgeLanServer.WpfConfigManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        _viewModel.ThemeChanged += OnThemeChanged;
        DataContext = _viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    /// <summary>
    /// Tự động nạp profile ngay khi bảng quản lý cấu hình mở.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        OnThemeChanged(_viewModel.Theme);
    }

    /// <summary>
    /// Lưu profile trước khi đóng để lần mở sau khôi phục đúng trạng thái.
    /// </summary>
    private async void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        await _viewModel.SaveProfilesAsync();
    }

    private void OnThemeChanged(ThemeMode theme)
    {
        if (Application.Current is App app)
        {
            app.ApplyTheme(theme);
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
