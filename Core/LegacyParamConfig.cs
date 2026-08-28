using NSC_ModManager.Model;

namespace NSC_ModManager_Android.Core;

internal static class LegacyParamConfig
{
    public static List<CharacterModModel> LoadCharacters(IEnumerable<ModInfo> mods)
    {
        var list = new List<CharacterModModel>();
        foreach (var mod in mods)
        {
            foreach (string cfg in Directory.EnumerateFiles(mod.RootPath, "character_config.ini", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var ini = new IniFile(cfg);
                string root = Path.GetDirectoryName(cfg)!;
                string game = ini.Read("Game", "ModManager", "NSC");
                int.TryParse(ini.Read("Page", "ModManager", "-1"), out int page);
                int.TryParse(ini.Read("Slot", "ModManager", "-1"), out int slot);
                int.TryParse(ini.Read("Page_NS4", "ModManager", "-1"), out int pageNs4);
                int.TryParse(ini.Read("Slot_NS4", "ModManager", "-1"), out int slotNs4);
                bool.TryParse(ini.Read("Partner", "ModManager", "false"), out bool partner);
                bool.TryParse(ini.Read("EnableRosterChange", "ModManager", "false"), out bool roster);
                bool.TryParse(ini.Read("EnableRosterChangeNS4", "ModManager", "false"), out bool rosterNs4);
                list.Add(new CharacterModModel
                {
                    Characode = Path.GetFileName(root), RootPath = root, GameVersion = string.IsNullOrWhiteSpace(game) ? "NSC" : game,
                    Page = page, Slot = slot, Page_NS4 = pageNs4, Slot_NS4 = slotNs4,
                    Partner = partner, EnableRosterChange = roster, EnableRosterChangeNS4 = rosterNs4
                });
            }
        }
        return list;
    }

    public static List<StageModModel> LoadStages(IEnumerable<ModInfo> mods)
    {
        var list = new List<StageModModel>();
        foreach (var mod in mods)
        {
            foreach (string cfg in Directory.EnumerateFiles(mod.RootPath, "stage_config.ini", SearchOption.AllDirectories)
                         .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                var ini = new IniFile(cfg);
                string root = Path.GetDirectoryName(cfg)!;
                int.TryParse(ini.Read("BGM_ID", "ModManager", "0"), out int bgm);
                int.TryParse(ini.Read("BGM_ID_NS4", "ModManager", bgm.ToString()), out int bgmNs4);
                bool.TryParse(ini.Read("Hell", "ModManager", "false"), out bool hell);
                string game = ini.Read("Game", "ModManager", "NSC");
                list.Add(new StageModModel
                {
                    StageName = Path.GetFileName(root), RootPath = root, BgmID = bgm, BgmID_NS4 = bgmNs4,
                    MessageID = ini.Read("MessageID", "ModManager"), Hell = hell,
                    GameVersion = string.IsNullOrWhiteSpace(game) ? "NSC" : game
                });
            }
        }
        return list;
    }
}
