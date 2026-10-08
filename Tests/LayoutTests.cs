using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SignalScheduler.Models;
using SignalScheduler.Presentation;
using SignalScheduler.ViewModels;
using SignalScheduler.Views;
using Xunit;

namespace SignalScheduler.Tests;

public sealed class LayoutTests
{
    [AvaloniaTheory]
    [InlineData(960, 640)]
    [InlineData(1120, 760)]
    [InlineData(1440, 900)]
    public void PanelsRemainSideBySideAndScrollWithoutMovingHeader(int width, int height)
    {
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        var content = view.Content;
        view.Content = null; // Do not open the production queue, Keychain or scheduler.
        using var host = new HostScope(new Window
        {
            Content = content, DataContext = model, Width = width, Height = height,
            Background = view.Background, RequestedThemeVariant = ThemeVariant.Dark
        });
        foreach (var style in view.Styles.ToArray())
        {
            view.Styles.Remove(style);
            host.Window.Styles.Add(style);
        }
        var messages = view.FindControl<ItemsControl>("UpcomingList")!;
        var fixtures = Enumerable.Range(0, 40).Select(index => new MessageViewModel(
            new ScheduledMessage(Guid.NewGuid(), "+12025550123", "Synthetic layout fixture " + index,
                DateTimeOffset.Now.AddHours(index + 1), MessageStatus.Pending,
                "+12025550100", "fixture-cli"), model)).ToArray();
        messages.ItemsSource = MessageQueuePresentation.Upcoming(fixtures, DateTime.Today);
        model.Recipient = "+12025550123";
        model.Body = "Synthetic layout preview";
        model.When = DateTime.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm");
        var date = view.FindControl<TextBox>("SendDatePicker")!;
        var time = view.FindControl<TextBox>("SendTimePicker")!;
        host.Window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(date.Bounds.Y, time.Bounds.Y);
        Assert.True(time.Bounds.X >= date.Bounds.Right);
        Assert.True(date.Bounds.Width > 0);
        Assert.True(time.Bounds.Width > 0);
        var compose = view.FindControl<Border>("ComposePanel")!;
        var queue = view.FindControl<Border>("MessagesPanel")!;
        var header = view.FindControl<Border>("ApplicationHeader")!;
        var composeScroll = view.FindControl<ScrollViewer>("ComposeScroll")!;
        var queueScroll = view.FindControl<ScrollViewer>("MessagesScroll")!;
        Assert.True(compose.Bounds.Width >= 350);
        Assert.True(queue.Bounds.Width >= 480);
        Assert.True(queue.Bounds.X >= compose.Bounds.Right);
        Assert.True(composeScroll.Viewport.Height > 0);
        Assert.True(queueScroll.Extent.Height > queueScroll.Viewport.Height);
        var headerBounds = header.Bounds;
        var composeOffset = composeScroll.Offset;
        queueScroll.Offset = queueScroll.Offset.WithY(200);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(headerBounds, header.Bounds);
        Assert.Equal(composeOffset, composeScroll.Offset);
        Assert.DoesNotContain(host.Window.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "Refresh accounts"));
        var selector = view.FindControl<ComboBox>("FromAccountSelector")!;
        Assert.Contains(header, selector.GetVisualAncestors());
        Assert.Equal(960, view.MinWidth);
        Assert.Equal(640, view.MinHeight);
        // Optional real Avalonia rendering with synthetic fixtures, not a production account.
        if (Environment.GetEnvironmentVariable("SIGNALSCHEDULER_LAYOUT_PREVIEW") is { } directory)
        {
            Directory.CreateDirectory(directory);
            using var frame = host.Window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(directory, $"layout-{width}x{height}.png"));
        }
        foreach (var fixture in fixtures) fixture.Dispose();
        model.TryClose();
    }

    [AvaloniaFact]
    public async Task EmptyAccountStateKeepsQrLinkAndRefreshInSettingsAvailable()
    {
        using var queue = new TestQueue();
        File.WriteAllText(queue.Executable, "#!/bin/sh\nif [ \"$1\" = '--version' ]; then printf 'signal-cli 0.14.9'; else printf '[]'; fi\n");
        File.SetUnixFileMode(queue.Executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var view = new MainWindow();
        var model = (MainWindowViewModel)view.DataContext!;
        await model.Signal.ConfigureExecutableAsync(queue.Executable, persist: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.FindControl<Border>("SignalWelcome")!.IsVisible);
        Assert.False(view.FindControl<Border>("MessageComposer")!.IsVisible);
        Assert.True(view.FindControl<Button>("ConnectSignalButton")!.IsEnabled);
        Assert.True(view.FindControl<Button>("SettingsButton")!.IsEnabled);
        using var settings = new HostScope(new SignalCliSettingsWindow(model.Signal));
        settings.Window.Show();
        Dispatcher.UIThread.RunJobs();
        var refresh = settings.Window.FindControl<Button>("RefreshAccountsButton")!;
        Assert.Same(model.Signal.RefreshAccountsCommand, refresh.Command);
        Assert.True(refresh.IsEffectivelyEnabled);
        model.TryClose();
    }

    private sealed class HostScope(Window window) : IDisposable
    {
        public Window Window => window;
        public void Dispose() => Window.Close();
    }
}
