namespace NSC_ModManager_Android.Core;

public static class PathValidator
{
    public static (bool Ok, string Message) ValidateGamePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "Game path is empty.");
        if (!Directory.Exists(path)) return (false, "Directory does not exist or Android cannot access it.");
        string[] markers = { "NSUNSC.exe", "NSUNS4.exe", "data_win32", "data" };
        bool marker = markers.Any(x => File.Exists(System.IO.Path.Combine(path, x)) || Directory.Exists(System.IO.Path.Combine(path, x)));
        return marker ? (true, "Game directory looks accessible.") : (true, "Directory is accessible, but no known Storm marker was found.");
    }
}
