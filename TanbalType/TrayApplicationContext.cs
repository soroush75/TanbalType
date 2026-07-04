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
    private readonly string AppVersion = "1.0.0";
    private readonly string AppDeveloper = "سروش سرمست";

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

        _enabledItem = new ToolStripMenuItem("فعال", null, ToggleEnabled) { Checked = true };
        _logEnabledItem = new ToolStripMenuItem("ذخیره log", null, ToggleLogging) { Checked = AppLog.IsEnabled };
        
        // تنظیم دکمه استارت‌آپ بر اساس وجود فایل Shortcut در پوشه Startup
        _startupItem = new ToolStripMenuItem("اجرا در استارت‌آپ", null, ToggleStartup) { Checked = IsStartupEnabled() };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_logEnabledItem);
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

        try
        {
            _hook.Install();
            AppLog.Write("Hook installed successfully.");
            _trayIcon.ShowBalloonTip(
                6000,
                AppName,
                $"فعال شد.\nLog:\n{AppLog.PrimaryPath}",
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

    private void ToggleEnabled(object? sender, EventArgs e)
    {
        _service.Enabled = !_service.Enabled;
        _enabledItem.Checked = _service.Enabled;
        _enabledItem.Text = _service.Enabled ? "فعال" : "غیرفعال";
        _trayIcon.Text = _service.Enabled ? $"{AppName} — فعال" : $"{AppName} — غیرفعال";
        AppLog.Write(_service.Enabled ? "Enabled" : "Disabled");
    }

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