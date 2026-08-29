using Android.Content;

namespace NSC_ModManager_Android.Core;

public enum GameAccessMode
{
    DirectPath = 0,
    SafDocumentTree = 1,
    RootPath = 2
}

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

    public string GameTreeUri
    {
        get => _prefs.GetString("game_tree_uri", "") ?? "";
        set => Save("game_tree_uri", value);
    }

    public string GameDisplayPath
    {
        get => _prefs.GetString("game_display_path", "") ?? "";
        set => Save("game_display_path", value);
    }

    public GameAccessMode GameMode
    {
        get
        {
            if (_prefs.Contains("game_access_mode"))
                return (GameAccessMode)_prefs.GetInt("game_access_mode", (int)GameAccessMode.DirectPath);
            return _prefs.GetBoolean("root_game_path", false) ? GameAccessMode.RootPath : GameAccessMode.DirectPath;
        }
        set
        {
            using ISharedPreferencesEditor? editor = _prefs.Edit();
            if (editor is null) throw new InvalidOperationException("Unable to edit Android SharedPreferences.");
            editor.PutInt("game_access_mode", (int)value);
            editor.PutBoolean("root_game_path", value == GameAccessMode.RootPath); // migration compatibility
            editor.Apply();
        }
    }

    public bool RootGamePath
    {
        get => GameMode == GameAccessMode.RootPath;
        set
        {
            if (value) GameMode = GameAccessMode.RootPath;
            else if (GameMode == GameAccessMode.RootPath) GameMode = GameAccessMode.DirectPath;
        }
    }

    public bool SafGamePath
    {
        get => GameMode == GameAccessMode.SafDocumentTree;
        set
        {
            if (value) GameMode = GameAccessMode.SafDocumentTree;
            else if (GameMode == GameAccessMode.SafDocumentTree) GameMode = GameAccessMode.DirectPath;
        }
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
