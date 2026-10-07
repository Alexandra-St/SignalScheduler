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
    public override void Initialize() => Styles.Add(new FluentTheme());
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
    [InlineData(MessageStatus.Pending, "Status: Pending", true, true, false)]
    [InlineData(MessageStatus.Sending, "Status: Sending", false, false, false)]
    [InlineData(MessageStatus.Sent, "Status: Sent", false, true, true)]
    [InlineData(MessageStatus.Cancelled, "Status: Canceled", false, true, true)]
    [InlineData(MessageStatus.Unknown, "Status: Unknown", false, true, true)]
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
        var buttons = host.Window.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Equal(cancel, buttons.Single(button => Equals(button.Content, "Cancel")).IsVisible);
        Assert.Equal(reuse, buttons.Single(button => Equals(button.Content, "Use as new message")).IsVisible);
        Assert.Equal(delete, buttons.Single(button => Equals(button.Content, "Delete")).IsVisible);
        if (reuse)
        {
            model.ReuseCommand.Execute(null);
            Assert.Equal(message.Text, owner.Body);
            Assert.Single(owner.Attachments);
        }
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
        var input = view.FindControl<TextBox>("SendAtInput")!;
        var error = view.FindControl<TextBlock>("SendAtError")!;
        var button = view.FindControl<Button>("ScheduleMessageButton")!;
        var notifications = 0;
        model.ScheduleCommand.CanExecuteChanged += (_, _) => notifications++;
        input.Text = "2026-10-07 23:011";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(input.Text, model.When);
        Assert.True(error.IsVisible);
        Assert.Contains("yyyy-MM-dd HH:mm", error.Text);
        Assert.True(input.Classes.Contains("invalid"));
        Assert.False(button.IsEffectivelyEnabled);
        Assert.False(model.ScheduleCommand.CanExecute(null));
        Assert.True(notifications > 0);
        model.Schedule(); // Direct activation still validates even without a real queue.
        Assert.True(model.HasSendTimeError);
        input.Text = DateTime.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        Dispatcher.UIThread.RunJobs();
        Assert.False(error.IsVisible);
        Assert.False(input.Classes.Contains("invalid"));
        Assert.Equal("", model.SendTimeError);
        model.TryClose();
    }

    private sealed class WindowScope(Window window) : IDisposable
    {
        public Window Window => window;
        public void Dispose() => window.Close();
    }
}
