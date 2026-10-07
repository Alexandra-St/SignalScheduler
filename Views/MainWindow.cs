using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;

namespace SignalScheduler.Views;

public sealed class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private readonly StackPanel rows = new() { Spacing = 8 };
    private readonly StackPanel attachmentRows = new() { Spacing = 6 };

    public MainWindow()
    {
        Title = "Signal Scheduler";
        Width = 640;
        Height = 760;
        DataContext = viewModel;
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 10 };
        Content = new ScrollViewer { Content = panel };
        panel.Children.Add(new TextBlock { Text = "Signal Scheduler", FontSize = 26 });
        panel.Children.Add(new TextBlock { Text = "Keep the app open and your Mac awake until messages are sent." });
        var settings = new Button { Content = "Open Settings" };
        settings.Click += async (_, _) => await OpenSettingsAsync();
        panel.Children.Add(settings);
        var executableStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
        executableStatus.Bind(TextBlock.TextProperty, new Binding(nameof(viewModel.ExecutableStatus)));
        panel.Children.Add(executableStatus);
        var accounts = new ComboBox { PlaceholderText = "Choose a detected account", HorizontalAlignment = HorizontalAlignment.Stretch };
        accounts.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(viewModel.LinkedAccounts)));
        accounts.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(viewModel.Account)) { Mode = BindingMode.OneWay });
        accounts.SelectionChanged += (_, _) =>
        {
            if (accounts.SelectedItem is string selected) viewModel.Account = selected;
        };
        AddField("From", accounts);
        var refreshAccounts = new Button { Content = "Refresh accounts" };
        refreshAccounts.Click += async (_, _) => await viewModel.RefreshAccountsAsync();
        panel.Children.Add(refreshAccounts);
        var accountStatus = new TextBlock { TextWrapping = TextWrapping.Wrap };
        accountStatus.Bind(TextBlock.TextProperty, new Binding(nameof(viewModel.AccountDiscoveryStatus)));
        panel.Children.Add(accountStatus);
        AddField("To", Editor(nameof(viewModel.Recipient), "Phone number with country code"));
        var body = Editor(nameof(viewModel.Body), "Write a message…");
        body.AcceptsReturn = true;
        body.Height = 100;
        body.TextWrapping = TextWrapping.Wrap;
        AddField("Message", body);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var addImages = new Button { Content = "Attach photos…" };
        var pasteImage = new Button { Content = "Paste screenshot" };
        addImages.Click += async (_, _) => await SelectImagesAsync();
        pasteImage.Click += async (_, _) => await viewModel.PasteImageAsync();
        actions.Children.Add(addImages);
        actions.Children.Add(pasteImage);
        panel.Children.Add(actions);
        panel.Children.Add(attachmentRows);
        AddField("Send at", Editor(nameof(viewModel.When), "yyyy-MM-dd HH:mm"));

        var schedule = new Button { Content = "Schedule message" };
        schedule.Bind(Button.IsEnabledProperty, new Binding(nameof(viewModel.CanSchedule)));
        schedule.Click += (_, _) => viewModel.Schedule();
        panel.Children.Add(schedule);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(viewModel.Status)));
        panel.Children.Add(status);
        panel.Children.Add(rows);

        viewModel.QueueChanged += RefreshQueue;
        viewModel.AttachmentsChanged += RefreshAttachments;
        Opened += async (_, _) => await viewModel.OpenAsync();
        Closing += (_, args) => args.Cancel = !viewModel.TryClose();

        void AddField(string label, Control field)
        {
            panel.Children.Add(new TextBlock { Text = label });
            panel.Children.Add(field);
        }
    }

    private async Task OpenSettingsAsync()
    {
        var panel = new StackPanel { Margin = new Thickness(24), Spacing = 12 };
        var window = new Window { Title = "Settings → signal-cli", Width = 560, Height = 360,
            Content = new ScrollViewer { Content = panel } };
        panel.Children.Add(new TextBlock { Text = "signal-cli", FontSize = 22 });
        var automatic = new CheckBox { Content = "Automatic detection", IsHitTestVisible = false, Focusable = false };
        automatic.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(viewModel.AutomaticDetection)) { Source = viewModel });
        panel.Children.Add(automatic);
        var path = new TextBox { IsReadOnly = true, Watermark = "No executable detected" };
        path.Bind(TextBox.TextProperty, new Binding(nameof(viewModel.Cli)) { Source = viewModel, Mode = BindingMode.OneWay });
        panel.Children.Add(new TextBlock { Text = "Detected / selected executable" });
        panel.Children.Add(path);
        var version = new TextBlock();
        version.Bind(TextBlock.TextProperty, new Binding(nameof(viewModel.ExecutableVersion)) { Source = viewModel, StringFormat = "Version: {0}" });
        panel.Children.Add(version);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(viewModel.ExecutableStatus)) { Source = viewModel });
        panel.Children.Add(status);
        var choose = new Button { Content = "Choose executable…" };
        choose.Bind(Button.IsEnabledProperty, new Binding(nameof(viewModel.CanConfigureExecutable)) { Source = viewModel });
        choose.Click += async (_, _) =>
        {
            try
            {
                var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                { Title = "Choose signal-cli executable", AllowMultiple = false });
                if (files.Count == 1 && files[0].TryGetLocalPath() is { } selected)
                    await viewModel.ConfigureExecutableAsync(selected);
            }
            catch { viewModel.ReportExecutableSelectionError(); }
        };
        panel.Children.Add(choose);
        var reset = new Button { Content = "Use automatic detection" };
        reset.Bind(Button.IsEnabledProperty, new Binding(nameof(viewModel.CanConfigureExecutable)) { Source = viewModel });
        reset.Click += async (_, _) => await viewModel.ConfigureExecutableAsync(null);
        panel.Children.Add(reset);
        await window.ShowDialog(this);
    }

    private static TextBox Editor(string property, string? watermark = null)
    {
        var editor = new TextBox { Watermark = watermark };
        editor.Bind(TextBox.TextProperty, new Binding(property) { Mode = BindingMode.TwoWay });
        return editor;
    }

    private async Task SelectImagesAsync()
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
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.gif", "*.heic", "*.heif" }
                    }
                }
            });
            await viewModel.AddImagesAsync(files);
        }
        catch (Exception exception) { viewModel.ReportImageError(exception); }
    }

    private void RefreshAttachments()
    {
        foreach (var row in attachmentRows.Children.OfType<StackPanel>())
            foreach (var image in row.Children.OfType<Image>()) (image.Source as IDisposable)?.Dispose();
        attachmentRows.Children.Clear();
        foreach (var attachment in viewModel.Attachments.ToList())
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            try
            {
                using var stream = new MemoryStream(attachment.Data);
                row.Children.Add(new Image { Source = new Bitmap(stream), Width = 90, Height = 70, Stretch = Stretch.Uniform });
            }
            catch { }
            row.Children.Add(new TextBlock { Text = attachment.Name, VerticalAlignment = VerticalAlignment.Center });
            var remove = new Button { Content = "Remove" };
            remove.Click += (_, _) => viewModel.RemoveAttachment(attachment);
            row.Children.Add(remove);
            attachmentRows.Children.Add(row);
        }
    }

    private void RefreshQueue()
    {
        rows.Children.Clear();
        foreach (var message in viewModel.Messages)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock
            {
                Text = $"{message.Recipient} · {message.Due.LocalDateTime:yyyy-MM-dd HH:mm} · {MessageStatusLabels.Format(message.State)}",
                TextWrapping = TextWrapping.Wrap
            });
            row.Children.Add(new TextBlock { Text = message.Text, TextWrapping = TextWrapping.Wrap });
            foreach (var attachment in message.Attachments ?? new())
                row.Children.Add(new TextBlock { Text = "📎 " + attachment.Name });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            if (message.State == MessageStatus.Pending)
                AddAction("Cancel", () => viewModel.Cancel(message));
            if (message.State != MessageStatus.Sending)
            {
                AddAction("Copy to composer", () => viewModel.CopyToComposer(message));
                if (message.State != MessageStatus.Pending)
                    AddAction("Delete", () => viewModel.Delete(message));
            }
            row.Children.Add(actions);
            rows.Children.Add(row);

            void AddAction(string label, Action action)
            {
                var button = new Button { Content = label, IsEnabled = !viewModel.IsBusy };
                button.Click += (_, _) => action();
                actions.Children.Add(button);
            }
        }
    }
}
