using System.Text;

namespace NSC_ModManager_Android.Core;

/// <summary>
/// Post-compile verification for UltimateStormAPI parameter payloads.  These
/// files are consumed directly by d3dcompiler_47.dll at game runtime rather
/// than from param_files.cpk, so a successful XFBIN pack alone does not prove
/// that advanced hooks (conditions, D-Pad specials, ougi/awakening) received
/// their metadata.
/// </summary>
internal static class SpecialApiVerifier
{
    internal sealed class Expectations
    {
        public HashSet<string> ConditionManagerNames { get; } = new(StringComparer.Ordinal);
        public List<(string Name, int CharacodeId)> SpecialConditions { get; } = new();
        public HashSet<int> OugiAwakeningIds { get; } = new();
        public int Count => ConditionManagerNames.Count + SpecialConditions.Count + OugiAwakeningIds.Count;
    }

    public static void Verify(string generatedApiParam, Expectations expected, CompileResult result)
    {
        result.SpecialApiChecksExpected = expected.Count;
        int passed = 0;

        string managerPath = Path.Combine(generatedApiParam, "conditionprmManager.xfbin");
        var managerNames = ReadConditionManagerNames(managerPath);
        foreach (string name in expected.ConditionManagerNames)
        {
            if (managerNames.Contains(name))
            {
                passed++;
                result.FeatureDetails.Add($"API verify OK: condition manager '{name}'");
            }
            else
            {
                result.Warnings.Add($"API verify FAILED: condition manager '{name}' is missing from generated conditionprmManager.xfbin");
            }
        }

        string specialPath = Path.Combine(generatedApiParam, "specialCondParam.xfbin");
        var special = ReadSpecialConditions(specialPath);
        foreach (var item in expected.SpecialConditions)
        {
            if (special.Any(x => x.Name == item.Name && x.CharacodeId == item.CharacodeId))
            {
                passed++;
                result.FeatureDetails.Add($"API verify OK: special condition '{item.Name}' -> characode {item.CharacodeId}");
            }
            else
            {
                result.Warnings.Add($"API verify FAILED: special condition '{item.Name}' for characode {item.CharacodeId} is missing/remapped incorrectly");
            }
        }

        string ougiPath = Path.Combine(generatedApiParam, "ougiAwakeningParam.xfbin");
        var ougiIds = ReadInt32List(ougiPath);
        foreach (int id in expected.OugiAwakeningIds)
        {
            if (ougiIds.Contains(id))
            {
                passed++;
                result.FeatureDetails.Add($"API verify OK: ougi/awakening characode {id}");
            }
            else
            {
                result.Warnings.Add($"API verify FAILED: characode {id} is absent from generated ougiAwakeningParam.xfbin");
            }
        }

        result.SpecialApiChecksPassed = passed;
        if (expected.Count > 0)
            result.FeatureDetails.Add($"API post-compile verification: {passed}/{expected.Count} checks passed");
    }

    private static HashSet<string> ReadConditionManagerNames(string path)
    {
        var output = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return output;
        byte[] data = File.ReadAllBytes(path);
        const int size = 0x70;
        for (int p = 0; p + size <= data.Length; p += size)
        {
            string name = ReadAsciiZ(data, p, 0x30);
            if (!string.IsNullOrWhiteSpace(name)) output.Add(name);
        }
        return output;
    }

    private static List<(string Name, int CharacodeId)> ReadSpecialConditions(string path)
    {
        var output = new List<(string, int)>();
        if (!File.Exists(path)) return output;
        byte[] data = File.ReadAllBytes(path);
        const int size = 0x20;
        for (int p = 0; p + size <= data.Length; p += size)
        {
            string name = ReadAsciiZ(data, p, 0x18);
            int id = BitConverter.ToInt32(data, p + 0x18);
            output.Add((name, id));
        }
        return output;
    }

    private static HashSet<int> ReadInt32List(string path)
    {
        var output = new HashSet<int>();
        if (!File.Exists(path)) return output;
        byte[] data = File.ReadAllBytes(path);
        for (int p = 0; p + 4 <= data.Length; p += 4)
            output.Add(BitConverter.ToInt32(data, p));
        return output;
    }

    private static string ReadAsciiZ(byte[] data, int offset, int max)
    {
        int end = offset;
        int limit = Math.Min(data.Length, offset + max);
        while (end < limit && data[end] != 0) end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }
}
