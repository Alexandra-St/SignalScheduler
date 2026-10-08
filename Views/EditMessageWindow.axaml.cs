using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public partial class EditMessageWindow : Window
{
    private readonly MainWindowViewModel owner;
    private readonly Guid messageId;
    public EditMessageWindow(MainWindowViewModel owner, ScheduledMessage message)
    {
        InitializeComponent();
        this.owner = owner;
        messageId = message.Id;
        this.FindControl<TextBox>("MessageText")!.Text = message.Text;
    }
    private void OnCancel(object? sender, RoutedEventArgs args) => Close();
    private void OnSave(object? sender, RoutedEventArgs args)
    {
        if (owner.SavePendingText(messageId, this.FindControl<TextBox>("MessageText")!.Text ?? "", out var error))
        { Close(); return; }
        var label = this.FindControl<TextBlock>("EditError")!;
        label.Text = error;
        label.IsVisible = true;
    }
}
