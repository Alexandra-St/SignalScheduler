using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public sealed partial class MainWindow : Window
{
    private async void OnMessageTapped(object? sender, TappedEventArgs args)
    {
        if (args.Source is Avalonia.Visual visual && visual.GetVisualAncestors().OfType<Button>().Any()) return;
        if (args.Source is Button) return;
        if (sender is Border { DataContext: MessageViewModel message })
        {
            args.Handled = true;
            await new MessageDetailsWindow(viewModel, message).ShowDialog(this);
        }
    }
    private async void OnMessageKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key != Key.Enter || sender is not Border { IsFocused: true, DataContext: MessageViewModel message }) return;
        args.Handled = true;
        await new MessageDetailsWindow(viewModel, message).ShowDialog(this);
    }
    private void OnDateInputLostFocus(object? sender, RoutedEventArgs args) => viewModel.NormalizeDateInput();
    private void OnTimeInputLostFocus(object? sender, RoutedEventArgs args) => viewModel.NormalizeTimeInput();
    private readonly MainWindowViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.DeleteConfirmationRequested += async message =>
        {
            var current = viewModel.Messages.SingleOrDefault(item => item.Message.Id == message.Id);
            if (current == null) return;
            var details = new MessageDetailsWindow(viewModel, current);
            details.ShowDeleteConfirmation();
            await details.ShowDialog(this);
        };
        viewModel.TextEditRequested += async message =>
            await new EditMessageWindow(viewModel, message).ShowDialog(this);
    }

    private async void OnOpened(object? sender, EventArgs args) => await viewModel.OpenAsync();
    private void OnClosing(object? sender, WindowClosingEventArgs args) => args.Cancel = !viewModel.TryClose();
    private async void OnConnectSignal(object? sender, RoutedEventArgs args)
        => await new SignalLinkWindow(viewModel.Signal).ShowDialog(this);

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
