using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;
namespace SignalScheduler.Views;
public partial class RescheduleWindow : Window
{
    private readonly MainWindowViewModel owner = null!;
    private readonly Guid id;
    public RescheduleWindow() => InitializeComponent();
    public RescheduleWindow(MainWindowViewModel owner, ScheduledMessage message) : this()
    {
        this.owner = owner; id = message.Id;
        DataContext = new MessageViewModel(message, owner);
        var future = DateTime.Now.AddMinutes(30);
        this.FindControl<TextBox>("NewDate")!.Text = future.ToString("dd.MM.yyyy");
        this.FindControl<TextBox>("NewTime")!.Text = future.ToString("HH:mm");
        Closed += (_, _) => ((MessageViewModel)DataContext!).Dispose();
    }
    private void OnCancel(object? sender, RoutedEventArgs args) => Close(false);
    private void OnConfirm(object? sender, RoutedEventArgs args)
    {
        if (owner.TryReschedule(id, this.FindControl<TextBox>("NewDate")!.Text ?? "",
            this.FindControl<TextBox>("NewTime")!.Text ?? "", out var error)) { Close(true); return; }
        var label = this.FindControl<TextBlock>("RescheduleError")!; label.Text = error; label.IsVisible = true;
    }
}
