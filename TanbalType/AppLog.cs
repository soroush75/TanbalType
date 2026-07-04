using System.Diagnostics;

namespace TanbalType;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static string? _primaryPath;
    private static bool _initFailed;

    // ویژگی جدید برای فعال/غیرفعال کردن ذخیره لاگ (پیش‌فرض: غیرفعال)
    public static bool IsEnabled { get; set; } = false;

    private static string ExeDirectory => System.AppContext.BaseDirectory;

    public static string PrimaryPath
    {
        get
        {
            EnsureInitialized();
            return _primaryPath ?? Path.Combine(Path.GetTempPath(), "TanbalType.log");
        }
    }

    public static IReadOnlyList<string> AllPaths { get; private set; } = [];

    public static void Initialize()
    {
        lock (Gate)
        {
            if (_primaryPath is not null)
                return;

            var candidates = new List<string>
            {
                Path.Combine(ExeDirectory, "TanbalType.log"),
                Path.Combine(Path.GetTempPath(), "TanbalType.log"),
            };

            try
            {
                candidates.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TanbalType",
                    "log.txt"));
            }
            catch
            {
                // ignore
            }

            AllPaths = candidates;
            _primaryPath = candidates[0];

            WriteInternal("=== TanbalType log started ===");
            WriteInternal($"BaseDirectory: {ExeDirectory}");
            WriteInternal($"ProcessPath: {Environment.ProcessPath ?? "(null)"}");
            WriteInternal($"User: {Environment.UserName}");
            WriteInternal($"Is64Bit: {Environment.Is64BitProcess}");
        }
    }

    public static void Write(string message) => WriteInternal(message);

    private static void EnsureInitialized()
    {
        if (_primaryPath is null)
            Initialize();
    }

    private static void WriteInternal(string message)
    {
        // در صورتی که کاربر گزینه ذخیره لاگ را فعال نکرده باشد، عملیات نوشتن متوقف می‌شود
        if (!IsEnabled)
            return;

        lock (Gate)
        {
            if (_primaryPath is null)
            {
                _primaryPath = Path.Combine(ExeDirectory, "TanbalType.log");
                AllPaths = [_primaryPath];
            }

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";
            var wroteAny = false;

            foreach (var path in AllPaths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(path, line);
                    wroteAny = true;
                }
                catch
                {
                    // try next path
                }
            }

            if (!wroteAny && !_initFailed)
            {
                _initFailed = true;
                try
                {
                    MessageBox.Show(
                        "نوشتن log ممکن نشد.\n" +
                        $"مسیر exe: {ExeDirectory}",
                        "TanbalType",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    public static void OpenPrimaryLog()
    {
        EnsureInitialized();
        foreach (var path in AllPaths)
        {
            if (!File.Exists(path))
                continue;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
            return;
        }

        var paths = string.Join(Environment.NewLine, AllPaths);
        MessageBox.Show(
            "فایل log پیدا نشد.\n\nمسیرهای بررسی‌شده:\n" + paths,
            "TanbalType",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}