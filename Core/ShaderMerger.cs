using System.Buffers.Binary;

namespace NSC_ModManager_Android.Core;

public static class ShaderMerger
{
    public static int BuildMergedFile(
        string gamePath,
        IReadOnlyList<ModInfo> mods,
        string stagedOutput,
        CompileResult result)
    {
        var shaders = mods
            .SelectMany(m => Directory.Exists(m.RootPath)
                ? Directory.EnumerateFiles(m.RootPath, "*.hlsl", SearchOption.AllDirectories)
                : Array.Empty<string>())
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (shaders.Length == 0) return 0;

        string target = Path.Combine(gamePath, "data", "system", "nuccMaterial_dx11.nsh");
        if (!File.Exists(target))
        {
            result.Warnings.Add($"{shaders.Length} shader file(s) found, but data/system/nuccMaterial_dx11.nsh is not accessible. Shader merge skipped.");
            return 0;
        }

        byte[] data = File.ReadAllBytes(target);
        if (data.Length < 0x10)
        {
            result.Warnings.Add("nuccMaterial_dx11.nsh is unexpectedly short. Shader merge skipped.");
            return 0;
        }

        short count = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(0x0E, 2));
        var used = new HashSet<uint>();
        int added = 0;

        foreach (string shader in shaders)
        {
            byte[] shaderData = File.ReadAllBytes(shader);
            if (shaderData.Length < 4)
            {
                result.Warnings.Add($"Shader '{Path.GetFileName(shader)}' is shorter than 4 bytes; skipped.");
                continue;
            }
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(shaderData.AsSpan(0, 4));
            if (!used.Add(key)) continue;

            int oldLength = data.Length;
            Array.Resize(ref data, oldLength + shaderData.Length);
            Buffer.BlockCopy(shaderData, 0, data, oldLength, shaderData.Length);
            count = checked((short)(count + 1));
            added++;
        }

        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x0E, 2), count);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x04, 4), data.Length);
        string? dir = Path.GetDirectoryName(stagedOutput);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(stagedOutput, data);
        return added;
    }

    public static void InstallMergedFile(string gamePath, string stagedOutput)
    {
        if (!File.Exists(stagedOutput)) return;
        string target = Path.Combine(gamePath, "data", "system", "nuccMaterial_dx11.nsh");
        string? dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        string backup = target + ".nscmm_android.bak";
        if (File.Exists(target) && !File.Exists(backup)) File.Copy(target, backup, false);
        File.Copy(stagedOutput, target, true);
    }
}
