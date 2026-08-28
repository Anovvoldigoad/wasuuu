namespace NSC_ModManager_Android.Core;

public sealed class CompileResult
{
    public int EnabledMods { get; internal set; }
    public int ResourceFiles { get; internal set; }
    public int CpkArchivesRead { get; internal set; }
    public int CpkArchivesPacked { get; internal set; }
    public int ShaderFiles { get; internal set; }
    public int ParameterXfbinsDetected { get; internal set; }
    public int CharacterConfigsDetected { get; internal set; }
    public int StageConfigsDetected { get; internal set; }
    public int ModelConfigsDetected { get; internal set; }
    public int ModdingApiFilesInstalled { get; internal set; }
    public int ParameterXfbinsMerged { get; internal set; }
    public int CharacterConfigsMerged { get; internal set; }
    public int StageConfigsMerged { get; internal set; }
    public int CharacterUiFilesGenerated { get; internal set; }
    public int StageUiFilesGenerated { get; internal set; }
    public int StageResourceXfbinsGenerated { get; internal set; }
    public int FixedRuntimeFilesGenerated { get; internal set; }
    public int BaseResourceFilesStaged { get; internal set; }
    public string? ReportPath { get; internal set; }
    public List<string> ParameterInputs { get; } = new();
    public List<string> Warnings { get; } = new();

    public string Summary =>
        $"Compiled {EnabledMods} mod(s) | resources {ResourceFiles} | CPK {CpkArchivesRead}->{CpkArchivesPacked} | shaders {ShaderFiles} | params {ParameterXfbinsMerged} merged | UI {CharacterUiFilesGenerated + StageUiFilesGenerated}";
}
