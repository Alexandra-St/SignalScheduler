using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public sealed partial class SignalCliSettingsWindow : Window
{
    private readonly SignalCliConfiguration configuration;

    public SignalCliSettingsWindow() : this(new SignalCliConfiguration()) { }

    public SignalCliSettingsWindow(SignalCliConfiguration configuration)
    {
        InitializeComponent();
        this.configuration = configuration;
        DataContext = configuration;
    }

    private async void OnChooseExecutable(object? sender, RoutedEventArgs args)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { Title = "Choose signal-cli executable", AllowMultiple = false });
            if (files.Count == 1 && files[0].TryGetLocalPath() is { } selected)
                await configuration.ConfigureExecutableAsync(selected);
        }
        catch { configuration.ReportSelectionError(); }
    }
}
