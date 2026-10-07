namespace SignalScheduler.Infrastructure;

public static class MacApplicationPaths
{
    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "SignalScheduler");

    public static string TemporaryDirectory => Path.Combine(DataDirectory, "temporary");

    public static string CreateTemporaryDirectory()
    {
        Directory.CreateDirectory(TemporaryDirectory);
        File.SetUnixFileMode(TemporaryDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var directory = Path.Combine(TemporaryDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.SetUnixFileMode(directory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return directory;
    }

    public static void DeleteTemporaryLeftovers()
    {
        if (Directory.Exists(TemporaryDirectory)) Directory.Delete(TemporaryDirectory, true);
    }
}
