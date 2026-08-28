using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NSC_ModManager.Compat;
using NSC_ModManager.UI;

namespace NSC_ModManager;

internal static class WinlatorEntry
{
    internal static readonly string StartupLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "winlator_startup.log");

    internal static void Trace(string message)
    {
        try
        {
            File.AppendAllText(StartupLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    private static void Main()
    {
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
        try { File.WriteAllText(StartupLogPath, string.Empty); } catch { }

        Trace("Main entered");
        Trace($"ProcessArchitecture={RuntimeInformation.ProcessArchitecture}; OSArchitecture={RuntimeInformation.OSArchitecture}; Is64BitProcess={Environment.Is64BitProcess}");
        Trace($"Framework={RuntimeInformation.FrameworkDescription}");
        Trace($"BaseDirectory={AppDomain.CurrentDomain.BaseDirectory}");

        Application.ThreadException += (_, e) => Trace("WinForms ThreadException: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Trace("UnhandledException: " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Trace("UnobservedTaskException: " + e.Exception);
            e.SetObserved();
        };

        try
        {
            Trace("Calling SetHighDpiMode(DpiUnaware)");
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Trace("SetHighDpiMode complete");

            Application.EnableVisualStyles();
            Trace("EnableVisualStyles complete");
            Application.SetCompatibleTextRenderingDefault(false);
            Trace("SetCompatibleTextRenderingDefault complete");

            System.Windows.Application.Current.Dispatcher.BindToCurrentThread();
            Trace("Compat dispatcher bound");

            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "YACpkTool.exe")))
                Trace("WARNING: YACpkTool.exe is missing; CPK operations will fail.");
            if (!File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CpkMaker.dll")))
                Trace("WARNING: CpkMaker.dll is missing; YACpkTool CPK operations will fail.");

            Trace("Creating MainForm");
            var mainForm = new MainForm();
            Trace("MainForm constructed; entering Application.Run");
            Application.Run(mainForm);
            Trace("Application.Run returned normally");
        }
        catch (Exception ex)
        {
            Trace("FATAL startup exception: " + ex);
            try { MessageBox.Show(ex.ToString(), "NSC Mod Manager startup error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }
}
