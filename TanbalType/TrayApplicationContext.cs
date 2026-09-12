namespace TanbalType;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _logEnabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly CorrectionService _service;
    private readonly KeyboardHook _hook;

    // مشخصات برنامه
    private readonly string AppName = "TanbalType";
    private readonly string AppDeveloper = "سروش سرمست";

    // نسخه از خودِ اسمبلی خوانده می‌شود (منبع واحد: تگ Version در TanbalType.csproj)
    private static string AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "?";

    // مسیر داینامیک پوشه Startup کاربر در ویندوز
    private string StartupShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        $"{AppName}.lnk");

    public TrayApplicationContext()
    {
        AppLog.Write("TrayApplicationContext constructor");

        var sync = SynchronizationContext.Current;
        _service = new CorrectionService(action =>
        {
            if (sync is not null)
                sync.Post(_ => action(), null);
            else
                Task.Run(action);
        });

        _hook = new KeyboardHook(_service);

        _enabledItem = new ToolStripMenuItem(EnabledItemText(true), null, ToggleEnabled) { Checked = true };
        _logEnabledItem = new ToolStripMenuItem("ذخیره log", null, ToggleLogging) { Checked = AppLog.IsEnabled };
        
        // تنظیم دکمه استارت‌آپ بر اساس وجود فایل Shortcut در پوشه Startup
        _startupItem = new ToolStripMenuItem("اجرا در استارت‌آپ", null, ToggleStartup) { Checked = IsStartupEnabled() };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_logEnabledItem);
        menu.Items.Add("لغات استثنا...", null, (_, _) => ShowExceptionsWindow());
        menu.Items.Add("لغات شخصی...", null, (_, _) => ShowUserWordsWindow());
        menu.Items.Add(_startupItem);
        menu.Items.Add("نمایش log", null, (_, _) => AppLog.OpenPrimaryLog());
        menu.Items.Add("درباره برنامه", null, ShowAboutWindow);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("خروج", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            Text = $"{AppName} — فعال",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _hook.ToggleRequested += OnToggleHotkey;

        try
        {
            _hook.Install();
            AppLog.Write("Hook installed successfully.");
            _trayIcon.ShowBalloonTip(
                6000,
                AppName,
                $"فعال شد.\nبا کلید F10 می‌توانید فعال/غیرفعال کنید.\nLog:\n{AppLog.PrimaryPath}",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Hook install FAILED: {ex}");
            MessageBox.Show(
                $"نصب keyboard hook ناموفق:\n{ex.Message}\n\n" +
                "Run as administrator را امتحان کنید.\n\n" +
                $"Log:\n{AppLog.PrimaryPath}",
                AppName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private void ToggleEnabled(object? sender, EventArgs e) =>
        SetEnabled(!_service.Enabled, notify: false);

    // میان‌بر روی همان نخی اجرا می‌شود که hook را نصب کرده (نخ UI)، پس دسترسی مستقیم به منو امن است.
    private void OnToggleHotkey() =>
        SetEnabled(!_service.Enabled, notify: true);

    private void SetEnabled(bool enabled, bool notify)
    {
        _service.Enabled = enabled;
        _enabledItem.Checked = enabled;
        _enabledItem.Text = EnabledItemText(enabled);
        _trayIcon.Text = enabled ? $"{AppName} — فعال" : $"{AppName} — غیرفعال";
        AppLog.Write(enabled ? "Enabled" : "Disabled");

        // با میان‌بر، تنها بازخوردِ کاربر همین اعلان است (منو باز نیست تا تیک را ببیند).
        if (notify)
            _trayIcon.ShowBalloonTip(
                2000,
                AppName,
                enabled ? "فعال شد." : "غیرفعال شد.",
                ToolTipIcon.Info);
    }

    private static string EnabledItemText(bool enabled) =>
        enabled ? "فعال (F10)" : "غیرفعال (F10)";

    private void ToggleLogging(object? sender, EventArgs e)
    {
        AppLog.IsEnabled = !AppLog.IsEnabled;
        _logEnabledItem.Checked = AppLog.IsEnabled;
        
        if (AppLog.IsEnabled)
            AppLog.Write("Log saving enabled by user.");
    }

    // --- متدهای مربوط به استارت‌آپ (با استفاده از Shortcut) ---
    
    private bool IsStartupEnabled()
    {
        // فقط چک می‌کنیم که فایل شورت‌کات در مسیر Startup وجود دارد یا خیر
        return File.Exists(StartupShortcutPath);
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        bool enable = !_startupItem.Checked;
        try
        {
            if (enable)
            {
                // ایجاد فایل Shortcut
                CreateShortcut();
                AppLog.Write("Shortcut created in Startup folder.");
            }
            else
            {
                // پاک کردن فایل Shortcut
                if (File.Exists(StartupShortcutPath))
                {
                    File.Delete(StartupShortcutPath);
                    AppLog.Write("Shortcut removed from Startup folder.");
                }
            }

            _startupItem.Checked = enable;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Failed to change startup setting: {ex.Message}");
            MessageBox.Show(
                "تغییر تنظیمات استارت‌آپ با خطا مواجه شد.\n" + ex.Message,
                "خطا",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void CreateShortcut()
    {
        // ساخت Shortcut با استفاده از WScript.Shell ویندوز بدون نیاز به رفرنس خارجی
        Type? t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) throw new Exception("WScript.Shell not found on this system.");
        
        dynamic shell = Activator.CreateInstance(t)!;
        var shortcut = shell.CreateShortcut(StartupShortcutPath);
        shortcut.TargetPath = Application.ExecutablePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
        shortcut.Description = AppName;
        shortcut.Save();
    }

    // ------------------------------------

    private ExceptionsForm? _exceptionsForm;

    private void ShowExceptionsWindow()
    {
        if (_exceptionsForm is { IsDisposed: false })
        {
            _exceptionsForm.Activate();
            return;
        }

        _exceptionsForm = new ExceptionsForm();
        _exceptionsForm.Show();
    }

    private UserWordsForm? _userWordsForm;

    private void ShowUserWordsWindow()
    {
        if (_userWordsForm is { IsDisposed: false })
        {
            _userWordsForm.Activate();
            return;
        }

        _userWordsForm = new UserWordsForm();
        _userWordsForm.Show();
    }

    private void ShowAboutWindow(object? sender, EventArgs e)
    {
        MessageBox.Show(
            $"{AppName}\n" +
            $"نسخه: {AppVersion}\n" +
            $"سازنده: {AppDeveloper}",
            $"درباره {AppName}",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hook.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            AppLog.Write("Application exit.");
        }

        base.Dispose(disposing);
    }
}