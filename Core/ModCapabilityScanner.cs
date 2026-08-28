namespace NSC_ModManager_Android.Core;

internal sealed record ModCapabilities(
    int CharacterConfigs,
    int StageConfigs,
    int ModelConfigs,
    int TujConfigs,
    int SpecialInteractionConfigs,
    int MessageFiles,
    int PrmFiles,
    int SpecialApiFiles,
    int CpkFiles,
    int ShaderFiles)
{
    public string Describe(string modName)
        => $"{modName}: character={CharacterConfigs}, stage={StageConfigs}, model={ModelConfigs}, TUJ={TujConfigs}, specialInteraction={SpecialInteractionConfigs}, message={MessageFiles}, PRM={PrmFiles}, specialAPI={SpecialApiFiles}, CPK={CpkFiles}, shader={ShaderFiles}";
}

internal static class ModCapabilityScanner
{
    private static readonly HashSet<string> SpecialApiNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "conditionprmManager.xfbin", "specialCondParam.xfbin", "partnerSlotParam.xfbin",
        "susanooCondParam.xfbin", "guardEffectParam.xfbin", "ougiAwakeningParam.xfbin",
        "gudoBallParam.xfbin", "pairSpSkillManagerParam.xfbin", "specialInteractionManager.xfbin",
        "bgmManagerParam.xfbin", "charRelationParam.xfbin"
    };

    internal static ModCapabilities Scan(ModInfo mod)
    {
        if (!Directory.Exists(mod.RootPath)) return new(0,0,0,0,0,0,0,0,0,0);

        int character = 0, stage = 0, model = 0, tuj = 0, specialInteraction = 0;
        int message = 0, prm = 0, specialApi = 0, cpk = 0, shader = 0;

        foreach (string path in Directory.EnumerateFiles(mod.RootPath, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            string ext = Path.GetExtension(path);

            if (name.Equals("character_config.ini", StringComparison.OrdinalIgnoreCase)) character++;
            else if (name.Equals("stage_config.ini", StringComparison.OrdinalIgnoreCase)) stage++;
            else if (name.Equals("model_config.ini", StringComparison.OrdinalIgnoreCase)) model++;
            else if (name.Equals("TUJ_config.ini", StringComparison.OrdinalIgnoreCase)) tuj++;
            else if (name.Equals("specialInteraction_config.ini", StringComparison.OrdinalIgnoreCase)) specialInteraction++;

            if (name.Equals("messageInfo.bin.xfbin", StringComparison.OrdinalIgnoreCase)) message++;
            if (ext.Equals(".cpk", StringComparison.OrdinalIgnoreCase)) cpk++;
            if (ext.Equals(".hlsl", StringComparison.OrdinalIgnoreCase)) shader++;

            if (name.EndsWith("prm.bin.xfbin", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("damageprm.bin.xfbin", StringComparison.OrdinalIgnoreCase)
                && !name.Equals("conditionprm.bin.xfbin", StringComparison.OrdinalIgnoreCase))
                prm++;

            if (SpecialApiNames.Contains(name)
                && path.Replace('\\','/').Contains("/moddingapi/", StringComparison.OrdinalIgnoreCase))
                specialApi++;
        }

        return new(character, stage, model, tuj, specialInteraction, message, prm, specialApi, cpk, shader);
    }
}
