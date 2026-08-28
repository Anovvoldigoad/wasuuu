using System.Buffers.Binary;

namespace NSC_ModManager_Android.Core;

public readonly record struct XfbinHeader(uint FileId, uint ChunkTableSize, uint MinPageSize, ushort Version, ushort VersionAttribute);

public static class XfbinPreflight
{
    public static readonly string[] ParameterKeywords =
    {
        "characode", "damageprm", "duelPlayerParam", "playerSettingParam", "skillCustomizeParam",
        "spSkillCustomizeParam", "characterSelectParam", "afterAttachObject", "costumeParam",
        "playerDoubleEffectParam", "cmnparam", "supportActionParam", "player_icon", "awakeAura",
        "appearanceAnm", "skillIndexSettingParam", "spTypeSupportParam", "privateCamera",
        "costumeBreakParam", "costumeBreakColorParam", "supportSkillRecoverySpeedParam",
        "damageeff", "effectprm", "StageInfo", "stageInfo", "messageInfo", "commandListParam",
        "Dictionary", "finalSpSkillCutIn", "flagprm", "hugeAwakeComboCameraParam",
        "meDecalParam", "situationVoice", "playerDecalSetting", "pairSpSkillCombinationParam"
    };

    public static bool IsParameterXfbin(string path)
    {
        if (!Path.GetExtension(path).Equals(".xfbin", StringComparison.OrdinalIgnoreCase))
            return false;
        return ParameterKeywords.Any(k => path.Contains(k, StringComparison.Ordinal));
    }

    public static bool TryReadHeader(string path, out XfbinHeader header, out string error)
    {
        header = default;
        error = string.Empty;
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> h = stackalloc byte[28];
            int read = stream.Read(h);
            if (read != h.Length)
            {
                error = $"too short ({read} bytes of header)";
                return false;
            }
            if (h[0] != (byte)'N' || h[1] != (byte)'U' || h[2] != (byte)'C' || h[3] != (byte)'C')
            {
                error = "missing NUCC magic";
                return false;
            }

            header = new XfbinHeader(
                BinaryPrimitives.ReadUInt32BigEndian(h.Slice(4, 4)),
                BinaryPrimitives.ReadUInt32BigEndian(h.Slice(16, 4)),
                BinaryPrimitives.ReadUInt32BigEndian(h.Slice(20, 4)),
                BinaryPrimitives.ReadUInt16BigEndian(h.Slice(24, 2)),
                BinaryPrimitives.ReadUInt16BigEndian(h.Slice(26, 2)));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
