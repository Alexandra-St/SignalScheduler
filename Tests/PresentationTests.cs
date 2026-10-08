using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SignalScheduler.Models;
using SignalScheduler.ViewModels;
using SignalScheduler.Views;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(SignalScheduler.Tests.TestAppBuilder))]

namespace SignalScheduler.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-GB");
        Styles.Add(new FluentTheme());
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class PresentationTests
{
    // Host only the view content: never open a real queue, Keychain, or scheduler in UI tests.
    private static Window Host(MainWindow view)
    {
        var content = view.Content;
        view.Content = null;
        var host = new Window { Content = content, DataContext = view.DataContext, Width = 640, Height = 760 };
        host.Styles.Add(new StyleInclude(new Uri("avares://SignalScheduler/"))
        { Source = new Uri("avares://SignalScheduler/Views/PresentationStyles.axaml") });
        host.Show();
        Dispatcher.UIThread.RunJobs();
        return host;
    }

    [AvaloniaFact]
    public async Task AccountSelectorBindsBothWaysAndInvalidSelectionDisablesScheduling()
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.1'; else printf '[{\"number\":\"account-one\"},{\"number\":\"account-two\"}]'; fi\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        using var host = new WindowScope(Host(view));
        await model.Signal.ConfigureExecutableAsync(queue.Executable, persist: false);
        Dispatcher.UIThread.RunJobs();
        var selector = view.FindControl<ComboBox>("FromAccountSelector")!;
        Assert.Null(selector.SelectedItem);
        selector.SelectedItem = "account-two";
        Assert.Equal("account-two", model.Signal.Account);
        Assert.True(model.Signal.HasSelectedAccount);
        model.Signal.Account = "account-one";
        Assert.Equal("account-one", selector.SelectedItem);
        selector.SelectedItem = null;
        Assert.False(model.Signal.HasSelectedAccount);
        Assert.False(model.ScheduleCommand.CanExecute(null));
        model.TryClose();
    }

    [AvaloniaFact]
    public void AttachmentsUseObservableBindingAndRemoveCommand()
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        using var host = new WindowScope(Host(view));
        var message = new ScheduledMessage(Guid.NewGuid(), "recipient-placeholder", "Synthetic", DateTimeOffset.Now.AddHours(1),
            MessageStatus.Sent, "account-placeholder", "/missing/old-cli", new() { new("synthetic.png", new byte[] { 1, 2, 3 }) });
        model.CopyToComposer(message);
        Dispatcher.UIThread.RunJobs();
        var list = view.FindControl<ItemsControl>("ComposerAttachments")!;
        Assert.Single(list.Items);
        var remove = host.Window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Remove"));
        remove.Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(list.Items);
        Assert.Empty(model.Attachments);
        Assert.Equal("Synthetic", model.Body);
        Assert.Equal("", model.Signal.Cli);
        model.TryClose();
    }

    [AvaloniaTheory]
    [InlineData(MessageStatus.Pending, "Pending", true, true, false)]
    [InlineData(MessageStatus.Sending, "Sending", false, false, false)]
    [InlineData(MessageStatus.Sent, "Sent", false, true, true)]
    [InlineData(MessageStatus.Cancelled, "Cancelled", false, true, true)]
    [InlineData(MessageStatus.Unknown, "Unknown", false, true, true)]
    public void MessageTemplateShowsStatusAndOnlyApplicableActions(MessageStatus state, string status,
        bool cancel, bool reuse, bool delete)
    {
        var view = new MainWindow();
        var owner = (MainWindowViewModel)view.DataContext!;
        var list = view.FindControl<ItemsControl>("MessagesList")!;
        var message = new ScheduledMessage(Guid.NewGuid(), "recipient-placeholder", "Synthetic", DateTimeOffset.Now.AddHours(1),
            state, "account-placeholder", "cli-placeholder", new() { new("synthetic.png", new byte[] { 1, 2, 3 }) });
        var model = new MessageViewModel(message, owner);
        var card = list.ItemTemplate!.Build(model)!;
        card.DataContext = model;
        using var host = new WindowScope(new Window { Content = card, Width = 640, Height = 400 });
        host.Window.Styles.Add(new StyleInclude(new Uri("avares://SignalScheduler/"))
        { Source = new Uri("avares://SignalScheduler/Views/PresentationStyles.axaml") });
        host.Window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(host.Window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == status);
        var button = host.Window.GetVisualDescendants().OfType<Button>().Single();
        Assert.Equal(cancel || reuse || delete, button.IsVisible);
        var menu = Assert.IsType<MenuFlyout>(button.Flyout);
        // Bindings are applied when the flyout is opened, exactly as in the application.
        if (button.IsVisible)
        {
            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            var items = menu.Items.OfType<MenuItem>().ToArray();
            Assert.Equal(state == MessageStatus.Pending, items.Single(item => Equals(item.Header, "Edit message text…")).IsVisible);
            Assert.Same(model.EditCommand, items.Single(item => Equals(item.Header, "Edit message text…")).Command);
            Assert.Equal(cancel, items.Single(item => Equals(item.Header, "Cancel message")).IsVisible);
            Assert.Equal(reuse, items.Single(item => Equals(item.Header, "Use as new message")).IsVisible);
            Assert.Equal(delete, items.Single(item => Equals(item.Header, "Delete from history")).IsVisible);
            Assert.Same(model.ReuseCommand, items.Single(item => Equals(item.Header, "Use as new message")).Command);
            menu.Hide();
        }
        if (reuse)
        {
            model.ReuseCommand.Execute(null);
            Assert.Equal(message.Text, owner.Body);
            Assert.Single(owner.Attachments);
        }
        model.Dispose();
        owner.TryClose();
    }

    [AvaloniaFact]
    public void MessageCardKeepsFullMultilineTextWithoutLineLimit()
    {
        var view = new MainWindow();
        var owner = (MainWindowViewModel)view.DataContext!;
        const string body = "First line\nSecond line\nThird line\n\nFinal line";
        using var model = new MessageViewModel(new ScheduledMessage(Guid.NewGuid(), "synthetic-recipient", body,
            DateTimeOffset.Now.AddHours(1), MessageStatus.Pending, "synthetic-account", "synthetic-cli"), owner);
        var card = view.FindControl<ItemsControl>("MessagesList")!.ItemTemplate!.Build(model)!;
        card.DataContext = model;
        using var host = new WindowScope(new Window { Content = card, Width = 640, Height = 400 });
        host.Window.Show();
        Dispatcher.UIThread.RunJobs();
        var text = host.Window.GetVisualDescendants().OfType<TextBlock>().Single(item => item.Text == body);
        Assert.Equal(0, text.MaxLines);
        Assert.Equal(Avalonia.Media.TextTrimming.None, text.TextTrimming);
        Assert.True(text.Bounds.Height > 60);
        owner.TryClose();
    }

    [AvaloniaFact]
    public void SettingsPathRemainsReadOnly()
    {
        var configuration = new SignalCliConfiguration();
        var window = new SignalCliSettingsWindow(configuration);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(Assert.Single(window.GetVisualDescendants().OfType<TextBox>()).IsReadOnly);
        window.Close();
        configuration.Close();
    }

    [AvaloniaFact]
    public void SendTimeErrorIsVisibleNextToInputAndClearsAfterCorrection()
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        using var host = new WindowScope(Host(view));
        var date = view.FindControl<TextBox>("SendDatePicker")!;
        var time = view.FindControl<TextBox>("SendTimePicker")!;
        var error = view.FindControl<TextBlock>("SendAtError")!;
        var button = view.FindControl<Button>("ScheduleMessageButton")!;
        date.Text = DateTime.Now.AddDays(-1).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        time.Text = "12:00";
        Dispatcher.UIThread.RunJobs();
        Assert.True(error.IsVisible);
        Assert.Contains("future", error.Text);
        Assert.False(button.IsEffectivelyEnabled);
        Assert.False(model.ScheduleCommand.CanExecute(null));
        model.Schedule(); // Direct activation still uses existing time validation.
        Assert.True(model.HasSendTimeError);
        date.Text = DateTime.Now.AddDays(2).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        time.Text = "14:35";
        Dispatcher.UIThread.RunJobs();
        Assert.False(error.IsVisible);
        Assert.Equal("", model.SendTimeError);
        Assert.EndsWith("14:35", model.When);
        Assert.Equal("14:35", time.Text);
        time.Text = "";
        Dispatcher.UIThread.RunJobs();
        Assert.True(error.IsVisible);
        time.Text = "14:36";
        Dispatcher.UIThread.RunJobs();
        Assert.False(error.IsVisible);
        model.TryClose();
    }

    [AvaloniaFact]
    public void SendAtKeyboardFieldsShowSeparateErrorAndFocusStates()
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        var content = view.Content;
        view.Content = null;
        using var host = new WindowScope(new Window { Content = content, DataContext = model,
            Width = 1120, Height = 760, Background = view.Background, RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark });
        foreach (var style in view.Styles.ToArray()) { view.Styles.Remove(style); host.Window.Styles.Add(style); }
        host.Window.Show();
        var date = view.FindControl<TextBox>("SendDatePicker")!;
        var time = view.FindControl<TextBox>("SendTimePicker")!;
        date.Text = DateTime.Today.AddDays(2).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        time.Text = "09:30";
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.HasSendTimeError);
        Capture("valid");
        date.Focus(); Dispatcher.UIThread.RunJobs();
        Assert.True(date.IsFocused); Capture("date-focus");
        time.Focus(); Dispatcher.UIThread.RunJobs();
        Assert.True(time.IsFocused); Capture("time-focus");
        date.Text = "05.10.2023"; Dispatcher.UIThread.RunJobs();
        Assert.Contains("invalid", date.Classes);
        Assert.DoesNotContain("invalid", time.Classes);
        Capture("past-date");
        date.Text = DateTime.Today.AddDays(2).ToString("dd.MM.yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
        time.Text = "25:30"; Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("invalid", date.Classes);
        Assert.Contains("invalid", time.Classes);
        model.Recipient = "Invalid recipient";
        Dispatcher.UIThread.RunJobs();
        var recipientError = view.FindControl<TextBlock>("RecipientError")!;
        var timeError = view.FindControl<TextBlock>("SendAtError")!;
        Assert.True(recipientError.IsVisible);
        Assert.Equal(recipientError.FontSize, timeError.FontSize);
        Assert.Equal(recipientError.FontFamily, timeError.FontFamily);
        Assert.Equal(recipientError.FontWeight, timeError.FontWeight);
        Assert.Equal(recipientError.Foreground!.ToString(), timeError.Foreground!.ToString());
        Assert.Null(view.FindControl<Avalonia.Controls.Shapes.Path>("SendAtErrorIcon"));
        Capture("invalid-time");
        model.TryClose();
        void Capture(string state)
        {
            if (Environment.GetEnvironmentVariable("SIGNALSCHEDULER_LAYOUT_PREVIEW") is not { } directory) return;
            Directory.CreateDirectory(directory);
            using var frame = host.Window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(System.IO.Path.Combine(directory, "send-at-" + state + ".png"));
        }
    }

    [AvaloniaFact]
    public void EmptyRecipientIsQuietUntilSchedulingIsAttempted()
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        using var host = new WindowScope(Host(view));
        var error = view.FindControl<TextBlock>("RecipientError")!;
        Dispatcher.UIThread.RunJobs();
        Assert.False(error.IsVisible);
        Assert.False(model.HasRecipientError);
        Assert.False(model.CanSchedule);
        model.Schedule();
        Dispatcher.UIThread.RunJobs();
        Assert.True(error.IsVisible);
        model.TryClose();
    }

    [AvaloniaFact]
    public void RecipientErrorClearsForAllSupportedFormatsAndRejectsNicknameWithoutSuffix()
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        using var host = new WindowScope(Host(view));
        var input = view.FindControl<TextBox>("RecipientInput")!;
        var error = view.FindControl<TextBlock>("RecipientError")!;
        input.Text = "Test_User";
        Dispatcher.UIThread.RunJobs();
        Assert.True(error.IsVisible);
        Assert.Equal(SignalScheduler.Integrations.Signal.SignalRecipient.Error, error.Text);
        Assert.False(model.ScheduleCommand.CanExecute(null));
        foreach (var value in new[] { "+12025550123", "Test_User.27", "https://signal.me/#eu/synthetic_link" })
        {
            input.Text = value;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(value, model.Recipient);
            Assert.False(error.IsVisible);
            Assert.False(model.HasRecipientError);
            Assert.DoesNotContain("invalid", input.Classes);
        }
        model.TryClose();
    }

    private sealed class WindowScope(Window window) : IDisposable
    {
        public Window Window => window;
        public void Dispose() => window.Close();
    }
}
