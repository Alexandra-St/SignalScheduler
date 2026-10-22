using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;
namespace SignalScheduler.Views;
public partial class RescheduleWindow : Window
{
    private readonly MainWindowViewModel owner = null!;
    private readonly Guid id;
    private Func<Task<bool>>? identityStillVerified;
    private bool scheduleAgain;
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
    public RescheduleWindow(MainWindowViewModel owner, ScheduledMessage message, Func<Task<bool>> identityStillVerified) : this()
    {
        this.owner = owner; id = message.Id; this.identityStillVerified = identityStillVerified; scheduleAgain = true;
        DataContext = new MessageViewModel(message, owner);
        var future = DateTime.Now.AddMinutes(30);
        this.FindControl<TextBox>("NewDate")!.Text = future.ToString("dd.MM.yyyy");
        this.FindControl<TextBox>("NewTime")!.Text = future.ToString("HH:mm");
        Title = "Schedule message again";
        this.FindControl<TextBlock>("DialogTitle")!.Text = "Schedule message again";
        this.FindControl<TextBlock>("PreserveHistoryNote")!.Text = "The original message will remain in History.";
        this.FindControl<Button>("ConfirmButton")!.Content = "Schedule again";
        Closed += (_, _) => ((MessageViewModel)DataContext!).Dispose();
    }
    private void OnCancel(object? sender, RoutedEventArgs args) => Close(false);
    private async void OnConfirm(object? sender, RoutedEventArgs args)
    {
        if (scheduleAgain)
        {
            var confirm = this.FindControl<Button>("ConfirmButton")!;
            confirm.IsEnabled = false;
            var result = await owner.TryScheduleAgainAsync(id, this.FindControl<TextBox>("NewDate")!.Text ?? "",
                this.FindControl<TextBox>("NewTime")!.Text ?? "", identityStillVerified!);
            if (result.Success) { Close(true); return; }
            var retryLabel = this.FindControl<TextBlock>("RescheduleError")!; retryLabel.Text = result.Error; retryLabel.IsVisible = true;
            confirm.IsEnabled = true;
            return;
        }
        if (owner.TryReschedule(id, this.FindControl<TextBox>("NewDate")!.Text ?? "",
            this.FindControl<TextBox>("NewTime")!.Text ?? "", out var error)) { Close(true); return; }
        var label = this.FindControl<TextBlock>("RescheduleError")!; label.Text = error; label.IsVisible = true;
    }
}
