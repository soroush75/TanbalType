namespace TanbalType;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly CorrectionService _service;
    private readonly KeyboardHook _hook;
    private readonly MouseHook _mouseHook;

    // منو تا بعدِ نمایانِ‌شدنِ آیکن ساخته نمی‌شود (ساختنش ~۱۱۰ms طول می‌کشد چون کل
    // پشتهٔ ToolStrip را برای اولین بار بار می‌کند)، پس این سه تا اول null‌اند.
    private ToolStripMenuItem? _enabledItem;
    private ToolStripMenuItem? _logEnabledItem;
    private ToolStripMenuItem? _startupItem;

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
        _mouseHook = new MouseHook(_service);

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = $"{AppName} — فعال",
            Visible = true,
        };

        // آیکن همین حالا نمایان می‌شود؛ منو در اولین بی‌کاریِ حلقهٔ پیام ساخته می‌شود
        // که همان لحظه‌های بعدِ آن است و کاربر فرصت نکرده راست‌کلیک کند.
        Application.Idle += BuildMenuOnFirstIdle;

        _hook.ToggleRequested += OnToggleHotkey;

        try
        {
            _hook.Install();
            AppLog.Write("Hook installed successfully.");

            // پاییدنِ کلیک برای فهمیدنِ جابه‌جاییِ مکان‌نما؛ اگر نصب نشود برنامه
            // مثل قبل کار می‌کند و خودش خطا را log می‌کند.
            _mouseHook.Install();
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

    private void BuildMenuOnFirstIdle(object? sender, EventArgs e)
    {
        Application.Idle -= BuildMenuOnFirstIdle;

        _enabledItem = new ToolStripMenuItem(EnabledItemText(_service.Enabled), null, ToggleEnabled)
            { Checked = _service.Enabled };
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

        _trayIcon.ContextMenuStrip = menu;
        AppLog.Write("Tray menu built.");
    }

    /// <summary>
    /// آیکن از منبعِ تعبیه‌شده خوانده می‌شود، نه با <c>Icon.ExtractAssociatedIcon</c>؛
    /// آن یکی فایل exe را از دیسک می‌خواند و از پوستهٔ ویندوز عبور می‌کند و ~۱۲۰ms می‌گیرد.
    /// </summary>
    private static Icon LoadAppIcon()
    {
        try
        {
            using var stream = typeof(TrayApplicationContext).Assembly
                .GetManifestResourceStream("TanbalType.app.ico");
            if (stream is not null)
                return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Embedded icon load failed: {ex.Message}");
        }

        return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    }

    private void ToggleEnabled(object? sender, EventArgs e) =>
        SetEnabled(!_service.Enabled, notify: false);

    // میان‌بر روی همان نخی اجرا می‌شود که hook را نصب کرده (نخ UI)، پس دسترسی مستقیم به منو امن است.
    private void OnToggleHotkey() =>
        SetEnabled(!_service.Enabled, notify: true);

    private void SetEnabled(bool enabled, bool notify)
    {
        _service.Enabled = enabled;

        // ممکن است میان‌بر F10 پیش از ساخته‌شدنِ منو زده شود؛ آن‌وقت تیکِ منو هنگام
        // ساختنش از روی وضعیتِ فعلیِ سرویس تنظیم می‌شود.
        if (_enabledItem is not null)
        {
            _enabledItem.Checked = enabled;
            _enabledItem.Text = EnabledItemText(enabled);
        }

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

        if (_logEnabledItem is not null)
            _logEnabledItem.Checked = AppLog.IsEnabled;

        if (AppLog.IsEnabled)
        {
            AppLog.Write("Log saving enabled by user.");

            // self-test دیگر هنگام راه‌اندازی اجرا نمی‌شود (با log خاموش خروجی‌اش دور
            // ریخته می‌شد و فقط بالاآمدن را عقب می‌انداخت). الان همان‌جا اجرا می‌شود که
            // به کار می‌آید: لحظه‌ای که کاربر log را برای عیب‌یابی روشن می‌کند.
            Task.Run(static () =>
            {
                try { DetectorSelfTest.RunAndLog(); }
                catch (Exception ex) { AppLog.Write($"SelfTest failed: {ex}"); }
            });
        }
    }

    // --- متدهای مربوط به استارت‌آپ (با استفاده از Shortcut) ---
    
    private bool IsStartupEnabled()
    {
        // فقط چک می‌کنیم که فایل شورت‌کات در مسیر Startup وجود دارد یا خیر
        return File.Exists(StartupShortcutPath);
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        bool enable = !IsStartupEnabled();
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

            if (_startupItem is not null)
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
            Application.Idle -= BuildMenuOnFirstIdle;
            _hook.Dispose();
            _mouseHook.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            AppLog.Write("Application exit.");
        }

        base.Dispose(disposing);
    }
}