using System.Windows.Input;

namespace SignalScheduler.Presentation;

// The operation owns error reporting; the command only prevents overlapping UI requests.
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter) => await ExecuteAsync();

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null)) return;
        running = true;
        NotifyCanExecuteChanged();
        try { await execute(); }
        finally { running = false; NotifyCanExecuteChanged(); }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
