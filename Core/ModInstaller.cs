using System.IO.Compression;

namespace NSC_ModManager_Android.Core;

public sealed class ModInstaller
{
    public string Install(string archivePath, string modsFolder)
    {
        if (!File.Exists(archivePath)) throw new FileNotFoundException("Mod archive not found", archivePath);
        Directory.CreateDirectory(modsFolder);
        string installFolder = System.IO.Path.Combine(modsFolder, System.IO.Path.GetFileNameWithoutExtension(archivePath));
        if (Directory.Exists(installFolder)) Directory.Delete(installFolder, true);
        Directory.CreateDirectory(installFolder);

        string ext = System.IO.Path.GetExtension(archivePath).ToLowerInvariant();
        if (ext == ".nus4") Nus4Extractor.Extract(archivePath, installFolder);
        else if (ext is ".nsc" or ".ensc" or ".uns" or ".unse") ZipFile.ExtractToDirectory(archivePath, installFolder, overwriteFiles: true);
        else throw new InvalidDataException($"Unsupported mod extension: {ext}");
        return installFolder;
    }
}
