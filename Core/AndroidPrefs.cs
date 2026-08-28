using Android.Content;

namespace NSC_ModManager_Android.Core;

public sealed class AndroidPrefs
{
    private readonly ISharedPreferences _prefs;

    public AndroidPrefs(Context context)
    {
        _prefs = context.GetSharedPreferences("nsc_mod_manager", FileCreationMode.Private)
                 ?? throw new InvalidOperationException("Unable to open Android SharedPreferences.");
    }

    public string GamePath
    {
        get => _prefs.GetString("game_path", "") ?? "";
        set => Save("game_path", value);
    }

    public string ModsPath
    {
        get => _prefs.GetString("mods_path", "/storage/emulated/0/NSC-ModManager/mods") ?? "";
        set => Save("mods_path", value);
    }

    private void Save(string key, string value)
    {
        using ISharedPreferencesEditor? editor = _prefs.Edit();
        if (editor is null)
            throw new InvalidOperationException("Unable to edit Android SharedPreferences.");
        editor.PutString(key, value);
        editor.Apply();
    }
}
