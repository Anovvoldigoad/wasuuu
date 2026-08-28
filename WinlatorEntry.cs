using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NSC_ModManager.Compat;
using NSC_ModManager.UI;

namespace NSC_ModManager;

internal static class WinlatorEntry
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [STAThread]
    private static void Main()
    {
        // A lot of the original backend intentionally uses Directory.GetCurrentDirectory().
        // Pin it to the application folder so launching from Wine/GameHub cannot break relative paths.
        Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Bind the WPF compatibility dispatcher to the WinForms UI thread so
        // legacy backend callbacks are marshalled safely during compilation.
        System.Windows.Application.Current.Dispatcher.BindToCurrentThread();

        try
        {
            if (LoadLibrary("MSVCP100.dll") == IntPtr.Zero)
            {
                UiBridge.Log("VC++ 2010 x86 runtime (MSVCP100.dll) was not detected. Install vcredist_x86.exe if CPK operations fail.");
            }
        }
        catch (Exception ex)
        {
            UiBridge.Log("VC++ runtime check skipped: " + ex.Message);
        }

        Application.Run(new MainForm());
    }
}
