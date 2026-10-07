using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public sealed partial class SignalLinkWindow : Window
{
    private readonly SignalLinkViewModel viewModel;
    private bool closing;
    public SignalLinkWindow() : this(new SignalCliConfiguration()) { }
    public SignalLinkWindow(SignalCliConfiguration configuration)
    {
        InitializeComponent();
        viewModel = new(configuration);
        DataContext = viewModel;
    }

    private async void OnOpened(object? sender, EventArgs args) => await viewModel.StartAsync();
    private async void OnClosing(object? sender, WindowClosingEventArgs args)
    {
        if (!viewModel.IsConnecting) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        await viewModel.CancelAsync();
        Close();
    }
    private void OnClose(object? sender, RoutedEventArgs args) => Close();
    private void OnClosed(object? sender, EventArgs args) => viewModel.Dispose();
}
