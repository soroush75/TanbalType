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
            DetectorSelfTest.RunAndLog();

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
