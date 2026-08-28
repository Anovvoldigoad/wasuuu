using Android.Content;

namespace NSC_ModManager_Android.Core;

public sealed class AndroidPrefs
{
    private readonly ISharedPreferences _prefs;
    public AndroidPrefs(Context context) => _prefs = context.GetSharedPreferences("nsc_mod_manager", FileCreationMode.Private)!;
    public string GamePath { get => _prefs.GetString("game_path", "") ?? ""; set => _prefs.Edit()!.PutString("game_path", value).Apply(); }
    public string ModsPath { get => _prefs.GetString("mods_path", "/storage/emulated/0/NSC-ModManager/mods") ?? ""; set => _prefs.Edit()!.PutString("mods_path", value).Apply(); }
}
