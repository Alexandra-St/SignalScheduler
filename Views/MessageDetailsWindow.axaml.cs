using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public partial class MessageDetailsWindow : Window
{
    private readonly MainWindowViewModel owner = null!;
    private readonly Guid id;
    private readonly List<AttachmentViewModel> attachments = new();
    public MessageDetailsWindow() => InitializeComponent();
    public MessageDetailsWindow(MainWindowViewModel owner, MessageViewModel message) : this()
    {
        this.owner = owner;
        id = message.Message.Id;
        Display(message);
        owner.PropertyChanged += OnOwnerChanged;
        Closed += (_, _) => { owner.PropertyChanged -= OnOwnerChanged; ClearAttachments(); };
    }
    private void OnOwnerChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MainWindowViewModel.Messages)) return;
        var current = owner.Messages.SingleOrDefault(item => item.Message.Id == id);
        if (current == null) { Close(); return; }
        Display(current);
    }
    private void Display(MessageViewModel message)
    {
        DataContext = message;
        ClearAttachments();
        attachments.AddRange(message.Attachments.Select(item => new AttachmentViewModel(item, _ => { })));
        this.FindControl<ItemsControl>("DetailsAttachments")!.ItemsSource = attachments.ToArray();
        SetDeleteConfirmation(false);
    }
    private void ClearAttachments()
    {
        foreach (var attachment in attachments) attachment.Dispose();
        attachments.Clear();
    }
    private async void OnReschedule(object? sender, RoutedEventArgs args)
    {
        var message = (MessageViewModel)DataContext!;
        if (!message.RescheduleCommand.CanExecute(null)) return;
        var dialog = new RescheduleWindow(owner, message.Message);
        if (await dialog.ShowDialog<bool>(this)) Close();
    }
    private async void OnEdit(object? sender, RoutedEventArgs args)
    {
        var message = (MessageViewModel)DataContext!;
        if (!message.EditCommand.CanExecute(null)) return;
        await new EditMessageWindow(owner, message.Message).ShowDialog(this);
    }
    private void OnClose(object? sender, RoutedEventArgs args) => Close();
    private void OnReuse(object? sender, RoutedEventArgs args)
    {
        var message = (MessageViewModel)DataContext!;
        if (!message.ReuseCommand.CanExecute(null)) return;
        message.ReuseCommand.Execute(null);
        Close();
    }
    private void SetDeleteConfirmation(bool visible)
    {
        this.FindControl<Border>("DeleteConfirmation")!.IsVisible = visible;
        this.FindControl<WrapPanel>("DetailsActions")!.IsVisible = !visible;
    }
    public void ShowDeleteConfirmation() => SetDeleteConfirmation(true);
    private void OnDelete(object? sender, RoutedEventArgs args)
    {
        if (((MessageViewModel)DataContext!).DeleteCommand.CanExecute(null))
            SetDeleteConfirmation(true);
    }
    private void OnKeepMessage(object? sender, RoutedEventArgs args)
        => SetDeleteConfirmation(false);
    private void OnConfirmDelete(object? sender, RoutedEventArgs args)
    {
        var message = (MessageViewModel)DataContext!;
        if (!message.DeleteCommand.CanExecute(null)) return;
        owner.Delete(message.Message);
        Close();
    }
}
