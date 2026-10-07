using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SignalScheduler;
static class Program
{
    [STAThread] public static void Main(string[] args) => AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
public sealed record Attachment(string Name, byte[] Data);
public sealed record Message(Guid Id, string Recipient, string Text, DateTimeOffset Due, string State, string Account, string Cli, List<Attachment>? Attachments = null);
public sealed class QueueStore : IDisposable
{
    readonly string path;
    readonly byte[] key;
    readonly FileStream instanceLock;
    public List<Message> Items { get; private set; } = new();
    public QueueStore(string directory, byte[] key)
    {
        Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        path = Path.Combine(directory, "queue.enc"); this.key = key;
        instanceLock = new FileStream(Path.Combine(directory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 28) throw new InvalidDataException("Queue is damaged. Original file was preserved.");
            var plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(bytes.AsSpan(0,12), bytes.AsSpan(28), bytes.AsSpan(12,16), plain);
            Items = JsonSerializer.Deserialize<List<Message>>(plain) ?? throw new InvalidDataException("Invalid queue");
            Items = Items.Select(m => m.State == "Sending" ? m with { State = "Unknown — check Signal before rescheduling" } : m).ToList();
            Save();
        }
    }
    public void Change(Guid id, string state) { Items = Items.Select(m => m.Id == id ? m with { State = state } : m).ToList(); Save(); }
    public void Save()
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(Items);
        var bytes = new byte[28 + plain.Length]; RandomNumberGenerator.Fill(bytes.AsSpan(0,12));
        using var aes = new AesGcm(key,16);
        aes.Encrypt(bytes.AsSpan(0,12),plain,bytes.AsSpan(28),bytes.AsSpan(12,16));
        var temp = path + ".tmp";
        using (var file = new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None))
        {
            File.SetUnixFileMode(temp, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            file.Write(bytes); file.Flush(true);
        }
        File.Move(temp,path,true);
    }
    public void Dispose() { CryptographicOperations.ZeroMemory(key); instanceLock.Dispose(); }
}
public static class Processes
{
    public static async Task<(int Code,string Output)> Run(string exe, IEnumerable<string> args, string? input = null)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true, RedirectStandardInput=true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var p = Process.Start(info) ?? throw new IOException("Could not start process");
        var output = p.StandardOutput.ReadToEndAsync(); var error = p.StandardError.ReadToEndAsync();
        if (input != null) await p.StandardInput.WriteAsync(input);
        p.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch { try { p.Kill(true); } catch { } throw; }
        await error; // Do not retain diagnostic text that may include message contents.
        return (p.ExitCode, await output);
    }
    public static async Task<byte[]> Key()
    {
        const string service = "local.SignalScheduler.queue";
        var found = await Run("/usr/bin/security",new[]{"find-generic-password","-s",service,"-a",Environment.UserName,"-w"});
        if (found.Code == 0) return Convert.FromBase64String(found.Output.Trim());
        if (found.Code != 44) throw new IOException("Keychain access failed. Unlock Keychain and reopen the app.");
        var key = RandomNumberGenerator.GetBytes(32);
        var added = await Run("/usr/bin/security",new[]{"add-generic-password","-s",service,"-a",Environment.UserName,"-w",Convert.ToBase64String(key)});
        if (added.Code != 0) throw new IOException("Could not save encryption key to Keychain.");
        return key;
    }
}
public sealed class MainWindow : Window
{
    readonly TextBox cli = new(){Text=File.Exists("/opt/homebrew/bin/signal-cli") ? "/opt/homebrew/bin/signal-cli" : "/usr/local/bin/signal-cli"};
    readonly TextBox account = new(){Watermark="Your account: +countrycode… or ACI"};
    readonly TextBox recipient = new(){Watermark="Recipient phone number: +countrycode…"};
    readonly TextBox body = new(){Watermark="Message",AcceptsReturn=true,Height=100,TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly TextBox when = new(){Text=DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm")};
    readonly TextBlock status = new(){Text="Opening encrypted queue…",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly StackPanel rows = new(){Spacing=8};
    readonly StackPanel attachmentRows = new(){Spacing=6};
    readonly List<Attachment> attachments = new();
    const int MaxBytes = 20 * 1024 * 1024;
    readonly Button schedule = new(){Content="Schedule",IsEnabled=false};
    readonly DispatcherTimer timer = new(){Interval=TimeSpan.FromSeconds(5)};
    QueueStore? store; bool busy, closing, faulted;
    public MainWindow()
    {
        Title="Signal Scheduler"; Width=640; Height=760;
        var panel = new StackPanel{Margin=new Thickness(24),Spacing=10};
        Content=new ScrollViewer{Content=panel};
        panel.Children.Add(new TextBlock{Text="Signal Scheduler",FontSize=26});
        panel.Children.Add(new TextBlock{Text="Local queue • keep this app open and your Mac awake"});
        Add("signal-cli executable",cli); Add("Linked account",account); Add("Recipient",recipient); Add("Message",body);
        var attachmentActions = new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        var addImages = new Button{Content="Add photos…"};
        var pasteImage = new Button{Content="Paste screenshot"};
        addImages.Click += async (_,_) => await AddImages();
        pasteImage.Click += async (_,_) => await PasteImage();
        attachmentActions.Children.Add(addImages); attachmentActions.Children.Add(pasteImage);
        panel.Children.Add(attachmentActions); panel.Children.Add(attachmentRows);
        Add("Send at — local time (yyyy-MM-dd HH:mm)",when);
        panel.Children.Add(schedule); panel.Children.Add(status); panel.Children.Add(rows);
        schedule.Click += (_,_) => Schedule();
        Opened += async (_,_) => {
            try {
                if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("This version requires macOS Keychain.");
                var temporary=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","SignalScheduler","temporary");
                store=new QueueStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","SignalScheduler"),await Processes.Key());
                if(Directory.Exists(temporary)) Directory.Delete(temporary,true);
                schedule.IsEnabled=true; status.Text="Ready. Account linking instructions are in README."; Refresh(); timer.Start();
            } catch(Exception e) { status.Text="Cannot open queue: " + e.Message; }
        };
        timer.Tick += async (_,_) => await Tick();
        Closing += (_,e) => { if(busy){e.Cancel=true;status.Text="Wait for the current send to finish before quitting.";} else {closing=true;timer.Stop();store?.Dispose();} };
        void Add(string label, Control field){panel.Children.Add(new TextBlock{Text=label});panel.Children.Add(field);}
    }
    void RefreshAttachments()
    {
        foreach(var row in attachmentRows.Children.OfType<StackPanel>())
            foreach(var image in row.Children.OfType<Image>()) (image.Source as IDisposable)?.Dispose();
        attachmentRows.Children.Clear();
        foreach(var a in attachments.ToList())
        {
            var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
            try {using var stream=new MemoryStream(a.Data); row.Children.Add(new Image{Source=new Bitmap(stream),Width=90,Height=70,Stretch=Avalonia.Media.Stretch.Uniform});} catch { }
            row.Children.Add(new TextBlock{Text=a.Name,VerticalAlignment=VerticalAlignment.Center});
            var remove=new Button{Content="Remove"}; remove.Click+=(_,_)=>{attachments.Remove(a);RefreshAttachments();};
            row.Children.Add(remove);attachmentRows.Children.Add(row);
        }
    }
    void AddAttachment(string name, byte[] data)
    {
        if(attachments.Count>=8 || data.Length==0 || attachments.Sum(a=>(long)a.Data.Length)+data.Length>MaxBytes)
            throw new IOException("Limit: 8 images, 20 MB total per message.");
        attachments.Add(new Attachment(Path.GetFileName(name),data));RefreshAttachments();status.Text="Image added.";
    }
    async Task AddImages()
    {
        try {
            var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions{
                Title="Choose photos or screenshots",AllowMultiple=true,
                FileTypeFilter=new[]{new FilePickerFileType("Images"){Patterns=new[]{"*.png","*.jpg","*.jpeg","*.webp","*.gif","*.heic","*.heif"}}}});
            foreach(var file in files) {
                await using var input=await file.OpenReadAsync();using var output=new MemoryStream();
                var buffer=new byte[81920];int count;
                while((count=await input.ReadAsync(buffer))>0) {if(output.Length+count>MaxBytes)throw new IOException("Image exceeds 20 MB.");output.Write(buffer,0,count);}
                AddAttachment(file.Name,output.ToArray());
            }
        } catch(Exception e) {status.Text="Could not add image: "+e.Message;}
    }
    async Task PasteImage()
    {
        // Native macOS clipboard bridge: PNG conversion also handles copied TIFF screenshots.
        try {
            // AppleScript writes PNG bytes directly; never pass image content as process arguments.
            var folder=CreateTempFolder();
            try {
                var path=Path.Combine(folder,"clipboard.png");
                var code="set targetFile to POSIX file "+JsonSerializer.Serialize(path)+"\nset imageData to the clipboard as «class PNGf»\nset handle to open for access targetFile with write permission\ntry\nset eof handle to 0\nwrite imageData to handle\nclose access handle\non error errMsg\ntry\nclose access handle\nend try\nerror errMsg\nend try";
                var result=await Processes.Run("/usr/bin/osascript",new[]{"-"},code);
                if(result.Code!=0 || !File.Exists(path))throw new IOException("Copy an image first (Control + Shift + Command + 4 for a screenshot).");
                if(new FileInfo(path).Length>MaxBytes)throw new IOException("Image exceeds 20 MB.");
                AddAttachment("Screenshot-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",await File.ReadAllBytesAsync(path));
            } finally {Directory.Delete(folder,true);}
        } catch(Exception e) {status.Text="Could not paste image: "+e.Message;}
    }
    static string CreateTempFolder()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","SignalScheduler","temporary");
        Directory.CreateDirectory(root);File.SetUnixFileMode(root,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        var folder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        File.SetUnixFileMode(folder,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);return folder;
    }
    static async Task<(int Code,string Output)> SendMessage(Message m)
    {
        if(m.Attachments==null || m.Attachments.Count==0)
            return await Processes.Run(m.Cli,new[]{"-a",m.Account,"send","--message-from-stdin",m.Recipient},m.Text);
        var folder=CreateTempFolder();
        try {
            var args=new List<string>{"-a",m.Account,"send","--message-from-stdin",m.Recipient,"--attachment"};
            for(var i=0;i<m.Attachments.Count;i++) {
                var a=m.Attachments[i];var path=Path.Combine(folder,i+"-"+Path.GetFileName(a.Name));
                await using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    File.SetUnixFileMode(path,UnixFileMode.UserRead|UnixFileMode.UserWrite);await f.WriteAsync(a.Data);
                }
                args.Add(path);
            }
            return await Processes.Run(m.Cli,args,m.Text);
        } finally {Directory.Delete(folder,true);}
    }
    void Schedule()
    {
        if(store==null || busy)return;
        if(!Regex.IsMatch(recipient.Text??"",@"^\+[1-9]\d{6,14}$")){status.Text="Enter recipient in international format.";return;}
        if(!File.Exists(cli.Text)||string.IsNullOrWhiteSpace(account.Text)){status.Text="Set executable path and linked account before scheduling.";return;}
        if(string.IsNullOrWhiteSpace(body.Text)&&attachments.Count==0){status.Text="Add text or a photo.";return;}
        if(!DateTime.TryParseExact(when.Text,"yyyy-MM-dd HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var date)){status.Text="Use yyyy-MM-dd HH:mm.";return;}
        if(TimeZoneInfo.Local.IsInvalidTime(date)||TimeZoneInfo.Local.IsAmbiguousTime(date)){status.Text="This time is ambiguous or skipped by daylight saving. Choose another time.";return;}
        var due=new DateTimeOffset(date,TimeZoneInfo.Local.GetUtcOffset(date));
        if(due<=DateTimeOffset.Now){status.Text="Choose a future time.";return;}
        try {store.Items.Add(new(Guid.NewGuid(),recipient.Text!,body.Text!,due,"Pending",account.Text!.Trim(),cli.Text!,attachments.ToList()));store.Save();body.Text="";attachments.Clear();RefreshAttachments();Refresh();status.Text="Scheduled.";}
        catch {schedule.IsEnabled=false;timer.Stop();status.Text="Queue write failed. Reopen the app before continuing.";}
    }
    void Refresh()
    {
        rows.Children.Clear(); if(store==null)return;
        foreach(var m in store.Items.OrderByDescending(m=>m.Due))
        {
            var row=new StackPanel{Spacing=4};
            row.Children.Add(new TextBlock{Text=$"{m.Recipient} · {m.Due.LocalDateTime:yyyy-MM-dd HH:mm} · {m.State}",TextWrapping=Avalonia.Media.TextWrapping.Wrap});
            row.Children.Add(new TextBlock{Text=m.Text,TextWrapping=Avalonia.Media.TextWrapping.Wrap});
            foreach(var a in m.Attachments ?? new()) row.Children.Add(new TextBlock{Text="📎 " + a.Name});
            var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
            if(m.State=="Pending") {
                var cancel=new Button{Content="Cancel",IsEnabled=!busy};
                cancel.Click+=(_,_)=>Mutate(()=>store.Change(m.Id,"Cancelled"));actions.Children.Add(cancel);
            }
            if(m.State!="Sending") {
                var copy=new Button{Content="Copy to composer",IsEnabled=!busy};
                copy.Click+=(_,_)=>{recipient.Text=m.Recipient;body.Text=m.Text;attachments.Clear();attachments.AddRange(m.Attachments ?? new());RefreshAttachments();account.Text=m.Account;cli.Text=m.Cli;when.Text=DateTime.Now.AddMinutes(30).ToString("yyyy-MM-dd HH:mm");}; actions.Children.Add(copy);
                if(m.State!="Pending") {var delete=new Button{Content="Delete",IsEnabled=!busy};delete.Click+=(_,_)=>Mutate(()=>{store.Items.RemoveAll(x=>x.Id==m.Id);store.Save();});actions.Children.Add(delete);}
            }
            row.Children.Add(actions);rows.Children.Add(row);
        }
    }
    void Mutate(Action action){try{action();Refresh();}catch{timer.Stop();schedule.IsEnabled=false;status.Text="Queue write failed. Reopen the app.";}}
    async Task Tick()
    {
        if(store==null||busy||closing)return;
        var m=store.Items.Where(x=>x.State=="Pending"&&x.Due<=DateTimeOffset.UtcNow).OrderBy(x=>x.Due).FirstOrDefault();if(m==null)return;
        if(DateTimeOffset.UtcNow-m.Due>TimeSpan.FromMinutes(5)){Mutate(()=>store.Change(m.Id,"Missed — reschedule manually"));return;}
        if(!File.Exists(m.Cli)){Mutate(()=>store.Change(m.Id,"Blocked — signal-cli missing; reschedule manually"));return;}
        busy=true;schedule.IsEnabled=false;var started=false;
        try {
            store.Change(m.Id,"Sending");Refresh();started=true;
            var result=await SendMessage(m);
            store.Change(m.Id,result.Code==0?"Sent — accepted by signal-cli":"Unknown/failed — check Signal before rescheduling");
            status.Text=result.Code==0?"Message sent.":"Send failed. Check Signal before scheduling a copy.";
        }catch{
            try { if(started)store.Change(m.Id,"Unknown — check Signal before rescheduling"); }catch{timer.Stop();faulted=true;}
            status.Text="Sending or saving failed. Check Signal before rescheduling; reopen the app.";
        }finally{busy=false;schedule.IsEnabled=!faulted;Refresh();}
    }
}
