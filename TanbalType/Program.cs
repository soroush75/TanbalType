namespace TanbalType;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            AppLog.Initialize();
            AppLog.Write("Main() entered");

            // لغت‌نامه (۳۳۲ هزار واژه) روی یک نخِ پس‌زمینه خوانده می‌شود تا آیکن tray
            // منتظرش نماند. تا کاربر اولین کلمه را تایپ کند خواندنش تمام شده است.
            // (پیش‌تر DetectorSelfTest روی همین مسیر بود و هر بار ~۱۰۰ms بالاآمدن را
            //  عقب می‌انداخت، آن هم برای خروجی‌ای که با log خاموش دور ریخته می‌شد؛
            //  حالا با روشن‌کردن log از منوی tray اجرا می‌شود.)
            Task.Run(static () =>
            {
                try
                {
                    Detector.Warmup();
                }
                catch (Exception ex)
                {
                    AppLog.Write($"Dictionary warmup failed: {ex}");
                }
            });

            ApplicationConfiguration.Initialize();
            AppLog.Write("ApplicationConfiguration.Initialize done");

            Application.Run(new TrayApplicationContext());
            AppLog.Write("Application.Run finished");
        }
        catch (Exception ex)
        {
            AppLog.Write($"FATAL: {ex}");
            MessageBox.Show(
                $"خطای بحرانی:\n{ex}\n\nLog:\n{AppLog.PrimaryPath}",
                "Keyboard Fix FA",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
