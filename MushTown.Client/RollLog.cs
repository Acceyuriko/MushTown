namespace MushTown.Client;

// Tiny rolling log for net messages: appends lines, rotates to .old past 1MB.
// Background socket tasks call it, so every write takes the lock.
public sealed class RollLog
{
    private readonly string _path;
    private readonly object _lock = new();

    public RollLog(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Write(string line)
    {
        lock (_lock)
        {
            if (File.Exists(_path) && new FileInfo(_path).Length > 1024 * 1024)
                File.Move(_path, _path + ".old", true);
            File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss} {line}{Environment.NewLine}");
        }
    }
}
