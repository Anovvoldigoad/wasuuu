namespace NSC_ModManager_Android.Core;

public sealed class ModInfo
{
    public required string RootPath { get; init; }
    public string Name { get; set; } = "Unknown Mod";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Game { get; set; } = "";
    public string LastUpdate { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string ConfigPath => System.IO.Path.Combine(RootPath, "mod_config.ini");
    public override string ToString() => $"{(Enabled ? "[ON]" : "[OFF]")} {Name}  {Version}".TrimEnd();
}
