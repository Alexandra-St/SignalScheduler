using Avalonia.Media.Imaging;
using Avalonia.Threading;
using QRCoder;
using SignalScheduler.Integrations.Signal;
using SignalScheduler.Presentation;

namespace SignalScheduler.ViewModels;

public sealed class SignalLinkViewModel : ObservableObject, IDisposable
{
    private readonly SignalCliConfiguration configuration;
    private readonly int timeoutSeconds;
    private CancellationTokenSource? cancellation;
    private Task? operation;
    private Bitmap? qrCode;
    private string status = "Connect Signal to start scheduling messages.";
    private bool connecting, connected;
    public Bitmap? QrCode { get => qrCode; private set { Set(ref qrCode, value); Notify(nameof(HasQrCode)); } }
    public bool HasQrCode => QrCode != null;
    public string Status { get => status; private set => Set(ref status, value); }
    public bool IsConnecting { get => connecting; private set => Set(ref connecting, value); }
    public bool Connected { get => connected; private set => Set(ref connected, value); }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand CancelCommand { get; }

    public SignalLinkViewModel(SignalCliConfiguration configuration, int timeoutSeconds = 180)
    {
        this.configuration = configuration;
        this.timeoutSeconds = timeoutSeconds;
        ConnectCommand = new(StartAsync, () => !IsConnecting && !Connected && configuration.CanConnect);
        CancelCommand = new(CancelAsync, () => IsConnecting);
    }

    public Task StartAsync()
    {
        if (IsConnecting || Connected || !configuration.TryBeginLink()) return Task.CompletedTask;
        cancellation = new CancellationTokenSource();
        ClearCode();
        IsConnecting = true;
        Status = "Preparing a secure connection…";
        NotifyCommands();
        operation = ConnectAsync(cancellation);
        return operation;
    }

    private async Task ConnectAsync(CancellationTokenSource run)
    {
        var succeeded = false;
        try
        {
            await SignalDeviceLinker.LinkAsync(configuration.Cli, async uri =>
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (run.IsCancellationRequested) return;
                    using var generator = new QRCodeGenerator();
                    using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
                    using var renderer = new PngByteQRCode(data);
                    using var stream = new MemoryStream(renderer.GetGraphic(8));
                    QrCode = new Bitmap(stream);
                    Status = "Scan this code with Signal on your phone, then approve the new device.";
                });
            }, run.Token, timeoutSeconds);
            succeeded = true;
            Status = "Checking your connected account…";
        }
        catch (OperationCanceledException) { Status = "Connection canceled. You can try again."; }
        catch (TimeoutException) { Status = "The code expired. Try again to get a new code."; }
        catch { Status = "Could not connect to Signal. Check your internet connection and try again."; }
        finally
        {
            ClearCode();
            configuration.EndLink();
            // Approval may have completed just as Cancel was pressed; reconcile account state.
            await configuration.RefreshAccountsAsync();
            if (succeeded)
            {
                Connected = configuration.LinkedAccounts.Count > 0;
                Status = Connected ? "Signal connected. You can close this window and schedule a message."
                    : "Connection finished, but the account could not be found. Refresh accounts before trying again.";
            }
            cancellation = null;
            run.Dispose();
            IsConnecting = false;
            NotifyCommands();
        }
    }

    public async Task CancelAsync()
    {
        cancellation?.Cancel();
        ClearCode();
        if (operation != null) await operation;
    }

    private void NotifyCommands()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private void ClearCode()
    {
        var old = QrCode;
        QrCode = null;
        old?.Dispose();
    }

    public void Dispose() { cancellation?.Cancel(); ClearCode(); }
}
