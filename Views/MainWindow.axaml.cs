using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void OnOpened(object? sender, EventArgs args) => await viewModel.OpenAsync();
    private void OnClosing(object? sender, WindowClosingEventArgs args) => args.Cancel = !viewModel.TryClose();
    private async void OnOpenSettings(object? sender, RoutedEventArgs args)
        => await new SignalCliSettingsWindow(viewModel.Signal).ShowDialog(this);

    private async void OnAttachPhotos(object? sender, RoutedEventArgs args)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose photos or screenshots",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Images")
                    { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.heic", "*.heif" } }
                }
            });
            await viewModel.AddImagesAsync(files);
        }
        catch (Exception exception) { viewModel.ReportImageError(exception); }
    }
}
