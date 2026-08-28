using System.Runtime.InteropServices;

namespace NSC_ModManager_Android.Core;

public static class NativeCpk
{
    [DllImport("cpkbridge", EntryPoint = "nsc_cpk_pack", CallingConvention = CallingConvention.Cdecl)]
    private static extern int PackNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputFolder,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputCpk,
        int compress,
        int mode);

    [DllImport("cpkbridge", EntryPoint = "nsc_cpk_extract", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExtractNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputCpk,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputFolder);

    public static int Pack(string inputFolder, string outputCpk, bool compress = false, int mode = 1)
        => PackNative(inputFolder, outputCpk, compress ? 1 : 0, mode);

    public static int Extract(string inputCpk, string outputFolder)
        => ExtractNative(inputCpk, outputFolder);
}
