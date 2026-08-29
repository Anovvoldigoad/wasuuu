using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using NSC_ModManager;
using NSC_ModManager.Model;
using NSC_ModManager.ViewModel;
using NSC_Toolbox.ViewModel;
using BinaryReader = NSC_ModManager.BinaryReader;

namespace NSC_ModManager_Android.Core;

internal sealed class LegacyParamCompiler
{
    public sealed record Output(
        string ParamFilesDirectory,
        string ModdingApiParamDirectory,
        string GameOverlayDirectory,
        int CharacterCount,
        int StageCount,
        int SavedXfbins,
        int CharacterUiFiles,
        int StageUiFiles,
        int StageResourceXfbins,
        int FixedOverlayFiles,
        int BaseResourceFiles);

    public Output Compile(
        IReadOnlyList<ModInfo> enabled,
        string baseParamZip,
        string messageBaseZip,
        string moddingApiPayloadZip,
        string root_folder,
        CompileResult result,
        Action<string>? progress = null)
    {
        progress ??= _ => { };
        string seed = Path.Combine(root_folder, "param_seed");
        string apiSeed = Path.Combine(root_folder, "api_seed");
        if (Directory.Exists(seed)) Directory.Delete(seed, true);
        if (Directory.Exists(apiSeed)) Directory.Delete(apiSeed, true);
        ZipFile.ExtractToDirectory(baseParamZip, seed, true);
        ZipFile.ExtractToDirectory(moddingApiPayloadZip, apiSeed, true);

        string baseNsc = Path.Combine(seed, "NSC");
        string baseNs4 = Path.Combine(seed, "NS4");
        string apiParam = Path.Combine(apiSeed, "moddingapi", "param", "NSC");
        string param_modmanager_path = Path.Combine(root_folder, "param_files");
        string generatedApiParam = Path.Combine(root_folder, "generated_moddingapi_param", "NSC");
        Directory.CreateDirectory(param_modmanager_path);
        Directory.CreateDirectory(generatedApiParam);

        List<CharacterModModel> CharacterList = LegacyParamConfig.LoadCharacters(enabled);
        List<StageModModel> StageList = LegacyParamConfig.LoadStages(enabled);
        result.CharacterConfigsMerged = CharacterList.Count;
        result.StageConfigsMerged = StageList.Count;

        bool hasMessageMods = CharacterList.Any(c => Directory.Exists(Path.Combine(c.RootPath, "data", "message")))
            || StageList.Any(s => Directory.Exists(Path.Combine(s.RootPath, "data", "message")));
        MessageInfoMerger.State? messageState = null;
        bool messageInfoModified = false;
        if (hasMessageMods)
        {
            progress("Semantic params: loading localization baseline...");
            messageState = MessageInfoMerger.LoadNscBaseline(messageBaseZip, root_folder);
        }

        progress($"Semantic params: loading vanilla baseline ({CharacterList.Count} character, {StageList.Count} stage config)...");

        string characodePath = Path.Combine(baseNsc, "characode.bin.xfbin");
        string duelPlayerParamPath = Path.Combine(baseNsc, "duelPlayerParam.xfbin");
        string playerSettingParamPath = Path.Combine(baseNsc, "playerSettingParam.bin.xfbin");
        string skillCustomizeParamPath = Path.Combine(baseNsc, "skillCustomizeParam.xfbin");
        string spSkillCustomizeParamPath = Path.Combine(baseNsc, "spSkillCustomizeParam.xfbin");
        string skillIndexSettingParamPath = Path.Combine(baseNsc, "skillIndexSettingParam.xfbin");
        string supportSkillRecoverySpeedParamPath = Path.Combine(baseNsc, "supportSkillRecoverySpeedParam.xfbin");
        string privateCameraPath = Path.Combine(baseNsc, "privateCamera.bin.xfbin");
        string characterSelectParamPath = Path.Combine(baseNsc, "characterSelectParam.xfbin");
        string costumeBreakColorParamPath = Path.Combine(baseNsc, "costumeBreakColorParam.xfbin");
        string costumeParamPath = Path.Combine(baseNsc, "costumeParam.bin.xfbin");
        string playerIconPath = Path.Combine(baseNsc, "player_icon.xfbin");
        string cmnparamPath = Path.Combine(baseNsc, "cmnparam.xfbin");
        string supportActionParamPath = Path.Combine(baseNsc, "supportActionParam.xfbin");
        string awakeAuraPath = Path.Combine(baseNsc, "awakeAura.xfbin");
        string appearanceAnmPath = Path.Combine(baseNsc, "appearanceAnm.xfbin");
        string afterAttachObjectPath = Path.Combine(baseNsc, "afterAttachObject.xfbin");
        string playerDoubleEffectParamPath = Path.Combine(baseNsc, "playerDoubleEffectParam.xfbin");
        string spTypeSupportParamPath = Path.Combine(baseNsc, "spTypeSupportParam.xfbin");
        string costumeBreakParamPath = Path.Combine(baseNsc, "costumeBreakParam.xfbin");
        string damageeffPath = Path.Combine(baseNsc, "damageeff.bin.xfbin");
        string damageeffS4Path = Path.Combine(baseNs4, "damageeff.bin.xfbin");
        string effectprmPath = Path.Combine(baseNsc, "effectprm.bin.xfbin");
        string damageprmPath = Path.Combine(baseNsc, "damageprm.bin.xfbin");
        string stageInfoPath = Path.Combine(baseNsc, "StageInfo.bin.xfbin");
        string conditionprmPath = Path.Combine(baseNsc, "conditionprm.bin.xfbin");

        string specialCondParamPath = Path.Combine(apiParam, "specialCondParam.xfbin");
        string partnerSlotParamPath = Path.Combine(apiParam, "partnerSlotParam.xfbin");
        string susanooCondParamPath = Path.Combine(apiParam, "susanooCondParam.xfbin");
        string conditionprmManagerPath = Path.Combine(apiParam, "conditionprmManager.xfbin");
        string guardEffectParamPath = Path.Combine(apiParam, "guardEffectParam.xfbin");
        string gudoBallParamPath = Path.Combine(apiParam, "gudoBallParam.xfbin");
        string ougiAwakeningParamPath = Path.Combine(apiParam, "ougiAwakeningParam.xfbin");

        CharacodeEditorViewModel characode_vanilla = new(); characode_vanilla.OpenFile(characodePath);
        DuelPlayerParamEditorViewModel duelPlayerParam_vanilla = new(); duelPlayerParam_vanilla.OpenFile(duelPlayerParamPath);
        PlayerSettingParamViewModel playerSettingParam_vanilla = new(); playerSettingParam_vanilla.OpenFile(playerSettingParamPath);
        SkillCustomizeParamViewModel skillCustomizeParam_vanilla = new(); skillCustomizeParam_vanilla.OpenFile(skillCustomizeParamPath);
        SpSkillCustomizeParamViewModel spSkillCustomizeParam_vanilla = new(); spSkillCustomizeParam_vanilla.OpenFile(spSkillCustomizeParamPath);
        SkillIndexSettingParamViewModel skillIndexSettingParam_vanilla = new(); skillIndexSettingParam_vanilla.OpenFile(skillIndexSettingParamPath);
        SupportSkillRecoverySpeedParamViewModel supportSkillRecoverySpeedParam_vanilla = new(); supportSkillRecoverySpeedParam_vanilla.OpenFile(supportSkillRecoverySpeedParamPath);
        PrivateCameraViewModel privateCamera_vanilla = new(); privateCamera_vanilla.OpenFile(privateCameraPath);
        CharacterSelectParamViewModel characterSelectParam_vanilla = new(); characterSelectParam_vanilla.OpenFile(characterSelectParamPath);
        CostumeBreakColorParamViewModel costumeBreakColorParam_vanilla = new(); costumeBreakColorParam_vanilla.OpenFile(costumeBreakColorParamPath);
        CostumeParamViewModel costumeParam_vanilla = new(); costumeParam_vanilla.OpenFile(costumeParamPath);
        PlayerIconViewModel playerIcon_vanilla = new(); playerIcon_vanilla.OpenFile(playerIconPath);
        cmnparamViewModel cmnparam_vanilla = new(); cmnparam_vanilla.OpenFile(cmnparamPath);
        SupportActionParamViewModel supportActionParam_vanilla = new(); supportActionParam_vanilla.OpenFile(supportActionParamPath);
        AwakeAuraViewModel awakeAura_vanilla = new(); awakeAura_vanilla.OpenFile(awakeAuraPath);
        AppearanceAnmViewModel appearanceAnm_vanilla = new(); appearanceAnm_vanilla.OpenFile(appearanceAnmPath);
        AfterAttachObjectViewModel afterAttachObject_vanilla = new(); afterAttachObject_vanilla.OpenFile(afterAttachObjectPath);
        PlayerDoubleEffectParamViewModel playerDoubleEffectParam_vanilla = new(); playerDoubleEffectParam_vanilla.OpenFile(playerDoubleEffectParamPath);
        SpTypeSupportParamViewModel spTypeSupportParam_vanilla = new(); spTypeSupportParam_vanilla.OpenFile(spTypeSupportParamPath);
        CostumeBreakParamViewModel costumeBreakParam_vanilla = new(); costumeBreakParam_vanilla.OpenFile(costumeBreakParamPath);
        DamageEffViewModel damageeff_vanilla = new(); damageeff_vanilla.OpenFile(damageeffPath);
        DamageEffViewModel damageeffS4_vanilla = new(); damageeffS4_vanilla.OpenFile(damageeffS4Path);
        EffectPrmViewModel effectprm_vanilla = new(); effectprm_vanilla.OpenFile(effectprmPath);
        DamagePrmViewModel damageprm_vanilla = new(); damageprm_vanilla.OpenFile(damageprmPath);
        StageInfoViewModel stageInfo_vanilla = new(); stageInfo_vanilla.OpenFile(stageInfoPath);
        ConditionPrmViewModel conditionprm_vanilla = new(); conditionprm_vanilla.OpenFile(conditionprmPath);
        ConditionManagerViewModel conditionprmManager_vanilla = new(); conditionprmManager_vanilla.OpenFile(conditionprmManagerPath);
        GuardEffectParamViewModel guardEffectParam_vanilla = new(); guardEffectParam_vanilla.OpenFile(guardEffectParamPath);

        byte[] specialCondParam_vanilla = File.ReadAllBytes(specialCondParamPath);
        byte[] partnerSlotParam_vanilla = File.ReadAllBytes(partnerSlotParamPath);
        byte[] susanooCondParam_vanilla = File.ReadAllBytes(susanooCondParamPath);
        byte[] ougiAwakeningParam_vanilla = File.ReadAllBytes(ougiAwakeningParamPath);
        byte[] gudoBallParam_vanilla = File.ReadAllBytes(gudoBallParamPath);

        int characode_count = characode_vanilla.CharacodeList.Count;
        bool stageInfoModified = false;
        List<StageModModel> StagesToAdd = new();
        List<string> CharselIconNamesList = new();
        List<string> CharselLoadedIconsList = new();
        var apiExpectations = new SpecialApiVerifier.Expectations();
        for (int i = 0; i < playerIcon_vanilla.playerIconList.Count; i++)
            if (!CharselLoadedIconsList.Contains(playerIcon_vanilla.playerIconList[i].BaseIcon))
                CharselLoadedIconsList.Add(playerIcon_vanilla.playerIconList[i].BaseIcon);

                //Compile Character mods
                foreach (CharacterModModel character_mod in CharacterList)
                {
                    string mod_characode = character_mod.Characode;
                    int mod_characodeID = -1;
                    bool replace_character = false;

                    //Read Characode file and add/find entry
                    foreach (CharacodeEditorModel entry in characode_vanilla.CharacodeList)
                    {
                        if (entry.CharacodeName == mod_characode)
                        {
                            mod_characodeID = entry.CharacodeIndex;
                            replace_character = true;
                            break;
                        }
                    }


                    // Required for adding
                    string duelPlayerParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "duelPlayerParam.xfbin");
                    string conditionprmModPath = Path.Combine(character_mod.RootPath, "data", "spc", "conditionprm.bin.xfbin");
                    string playerSettingParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "playerSettingParam.bin.xfbin");
                    string skillCustomizeParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "skillCustomizeParam.xfbin");
                    string spSkillCustomizeParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "spSkillCustomizeParam.xfbin");
                    string skillIndexSettingParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "skillIndexSettingParam.xfbin");
                    string supportSkillRecoverySpeedParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "supportSkillRecoverySpeedParam.xfbin");
                    string privateCameraModPath = Path.Combine(character_mod.RootPath, "data", "spc", "privateCamera.bin.xfbin");

                    string costumeParamModPath = Path.Combine(character_mod.RootPath, "data", "rpg", "param", "costumeParam.bin.xfbin");
                    string playerIconModPath = Path.Combine(character_mod.RootPath, "data", "spc", "player_icon.xfbin");
                    string cmnparamModPath = Path.Combine(character_mod.RootPath, "data", "sound", "cmnparam.xfbin");
                    string characterSelectParamModPath = Path.Combine(character_mod.RootPath, "data", "ui", "max", "select", "characterSelectParam.xfbin");
                    string supportActionParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "supportActionParam.xfbin");


                    //Not required for adding

                    string costumeBreakColorParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "costumeBreakColorParam.xfbin");
                    string awakeAuraModPath = Path.Combine(character_mod.RootPath, "data", "spc", "awakeAura.xfbin");
                    string appearanceAnmModPath = Path.Combine(character_mod.RootPath, "data", "spc", "appearanceAnm.xfbin");
                    string afterAttachObjectModPath = Path.Combine(character_mod.RootPath, "data", "spc", "afterAttachObject.xfbin");
                    string playerDoubleEffectParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "playerDoubleEffectParam.xfbin");
                    string spTypeSupportParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "spTypeSupportParam.xfbin");
                    string costumeBreakParamModPath = Path.Combine(character_mod.RootPath, "data", "spc", "costumeBreakParam.xfbin");
                    string messageInfoModPath = Path.Combine(character_mod.RootPath, "data", "message");
                    string damageeffModPath = Path.Combine(character_mod.RootPath, "data", "spc", "damageeff.bin.xfbin");
                    string effectprmModPath = Path.Combine(character_mod.RootPath, "data", "spc", "effectprm.bin.xfbin");
                    string damageprmModPath = Path.Combine(character_mod.RootPath, "data", "spc", "damageprm.bin.xfbin");

                    string specialCondParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "specialCondParam.xfbin");
                    string partnerSlotParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "partnerSlotParam.xfbin");
                    string susanooCondParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "susanooCondParam.xfbin");
                    string conditionprmManagerModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "conditionprmManager.xfbin");
                    if (!File.Exists(specialCondParamModPath))
                        specialCondParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "mods", "base_game", "specialCondParam.xfbin");
                    if (!File.Exists(partnerSlotParamModPath))
                        partnerSlotParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "mods", "base_game", "partnerSlotParam.xfbin");
                    if (!File.Exists(susanooCondParamModPath))
                        susanooCondParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "mods", "base_game", "susanooCondParam.xfbin");
                    if (!File.Exists(conditionprmManagerModPath))
                        conditionprmManagerModPath = Path.Combine(character_mod.RootPath, "moddingapi", "mods", "base_game", "conditionprmManager.xfbin");

                    string guardEffectParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "guardEffectParam.xfbin");
                    string ougiAwakeningParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param", "ougiAwakeningParam.xfbin");
                    string gudoBallParamModPath = Path.Combine(character_mod.RootPath, "moddingapi", "param","gudoBallParam.xfbin");

                    string stormVersion = character_mod.GameVersion;
                    //characode file
                    if (!replace_character)
                    {
                        //Check if files exists for partners/leaders in case if we add them instead of replacing
                        if (character_mod.Partner == false)
                        {
                            if (!File.Exists(duelPlayerParamModPath) ||
                            !File.Exists(playerSettingParamModPath) ||
                            !File.Exists(skillCustomizeParamModPath) ||
                            !File.Exists(spSkillCustomizeParamModPath) ||
                            //!File.Exists(skillIndexSettingParamModPath) ||
                            //!File.Exists(supportSkillRecoverySpeedParamModPath) ||
                            //!File.Exists(privateCameraModPath) ||
                            //!File.Exists(costumeParamModPath) ||
                            !File.Exists(playerIconModPath) ||
                            !File.Exists(cmnparamModPath) ||
                            !File.Exists(characterSelectParamModPath))
                            {
                                System.Windows.MessageBox.Show("Missing Param files");
                                continue;
                            }
                        } else
                        {
                            if (!File.Exists(duelPlayerParamModPath))
                            {
                                System.Windows.MessageBox.Show("Missing DuelPlayerParam file for partner");
                                continue;
                            }
                        }

                        //Add new code of character (leader/partner) into characode file
                        CharacodeEditorModel characode_entry = new CharacodeEditorModel();
                        characode_entry.CharacodeName = mod_characode;
                        mod_characodeID = characode_vanilla.CharacodeList.Count + 1;
                        characode_entry.CharacodeIndex = mod_characodeID;
                        characode_vanilla.CharacodeList.Add(characode_entry);



                    }

                    Dictionary<string, string> csp_code_replace = new Dictionary<string, string>();

                    /*---------------------------------------REQUIRED FILES-------------------------------------------*/
                    //duelPlayerParam file
                    List<string> baseModel = new List<string>();
                    List<string> awakeModel = new List<string>();
                    DuelPlayerParamEditorViewModel duelPlayerParam_mod = new DuelPlayerParamEditorViewModel();
                    if (File.Exists(duelPlayerParamModPath))
                    {
                        duelPlayerParam_mod.OpenFile(duelPlayerParamModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < duelPlayerParam_vanilla.DuelPlayerParamList.Count; i++)
                            {
                                if (duelPlayerParam_vanilla.DuelPlayerParamList[i].BinName.Contains(mod_characode))
                                {
                                    duelPlayerParam_vanilla.DuelPlayerParamList[i] = (DuelPlayerParamModel)duelPlayerParam_mod.DuelPlayerParamList[0].Clone();
                                    break;
                                }
                            }
                        } else
                        {
                            duelPlayerParam_vanilla.DuelPlayerParamList.Add((DuelPlayerParamModel)duelPlayerParam_mod.DuelPlayerParamList[0].Clone());
                        }
                        for (int i = 0; i < 20; i++)
                        {
                            if (!baseModel.Contains(duelPlayerParam_vanilla.DuelPlayerParamList[0].BaseCostumes[i].CostumeName) && duelPlayerParam_vanilla.DuelPlayerParamList[0].BaseCostumes[i].CostumeName != "")
                                baseModel.Add(duelPlayerParam_vanilla.DuelPlayerParamList[0].BaseCostumes[i].CostumeName);
                            if (!awakeModel.Contains(duelPlayerParam_vanilla.DuelPlayerParamList[0].AwakeCostumes[i].CostumeName) && duelPlayerParam_vanilla.DuelPlayerParamList[0].AwakeCostumes[i].CostumeName != "")
                                awakeModel.Add(duelPlayerParam_vanilla.DuelPlayerParamList[0].AwakeCostumes[i].CostumeName);
                        }

                    }

                    ConditionPrmViewModel conditionprm_mod = new ConditionPrmViewModel();
                    ConditionManagerViewModel conditionprmManager_mod = new ConditionManagerViewModel();
                    //conditionprm and conditionprmManager
                    if (File.Exists(conditionprmModPath) && File.Exists(conditionprmManagerModPath))
                    {
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: conditionprm + conditionprmManager");
                        conditionprm_mod.OpenFile(conditionprmModPath);
                        conditionprmManager_mod.OpenFile(conditionprmManagerModPath);


                        // Loop through all entries in the mod's ConditionList
                        foreach (ConditionPrmModel condition in conditionprm_mod.ConditionList)
                        {
                            // Check if the condition already exists in the vanilla list
                            var existingCondition = conditionprm_vanilla.ConditionList
                                .FirstOrDefault(c => c.ConditionName == condition.ConditionName);

                            if (existingCondition != null)
                            {
                                // Replace the existing condition with the new one
                                int index = conditionprm_vanilla.ConditionList.IndexOf(existingCondition);
                                conditionprm_vanilla.ConditionList[index] = (ConditionPrmModel)condition.Clone();
                            } else
                            {
                                // Add the condition if it does not exist
                                conditionprm_vanilla.ConditionList.Add((ConditionPrmModel)condition.Clone());
                            }
                        }

                        // Loop through all entries in the mod's ConditionManagerList
                        foreach (ConditionManagerModel conditionManager in conditionprmManager_mod.ConditionList)
                        {
                            if (!string.IsNullOrWhiteSpace(conditionManager.ConditionName))
                                apiExpectations.ConditionManagerNames.Add(conditionManager.ConditionName);
                            // Check if the condition manager already exists in the vanilla list
                            var existingConditionManager = conditionprmManager_vanilla.ConditionList
                                .FirstOrDefault(c => c.ConditionName == conditionManager.ConditionName);

                            if (existingConditionManager != null)
                            {
                                // Replace the existing condition manager with the new one
                                int index = conditionprmManager_vanilla.ConditionList.IndexOf(existingConditionManager);
                                conditionprmManager_vanilla.ConditionList[index] = (ConditionManagerModel)conditionManager.Clone();
                            } else
                            {
                                // Add the condition manager if it does not exist
                                conditionprmManager_vanilla.ConditionList.Add((ConditionManagerModel)conditionManager.Clone());
                            }
                        }
                    }


                    //playerSettingParam file
                    List<int> RemovedPresetIds = new List<int>();
                    List<int> AddedPresetIds = new List<int>();
                    List<string> RemovedCSPCodes = new List<string>();
                    bool IsPspModified = false;
                    string charMessageID = "";
                    PlayerSettingParamViewModel playerSettingParam_mod = new PlayerSettingParamViewModel();
                    PlayerSettingParamS4ViewModel playerSettingParamS4_mod = new PlayerSettingParamS4ViewModel();
                    if (File.Exists(playerSettingParamModPath))
                    {
                        switch (stormVersion)
                        {

                            case "NSC":
                                playerSettingParam_mod.OpenFile(playerSettingParamModPath);

                                foreach (PlayerSettingParamModel psp_entry in playerSettingParam_mod.PlayerSettingParamList)
                                {
                                    string costume_csp_code = psp_entry.PSP_code;
                                    int csp_code_index = 0;
                                    do
                                    {
                                        csp_code_index++;
                                        costume_csp_code = psp_entry.PSP_code + "_" + csp_code_index.ToString("D6");
                                    }
                                    while (playerSettingParam_vanilla.PSPCodeExists(costume_csp_code));

                                    csp_code_replace.Add(psp_entry.PSP_code, costume_csp_code);
                                    psp_entry.PSP_code = costume_csp_code;
                                }








                                if (replace_character)
                                {
                                    if (playerSettingParam_mod.PlayerSettingParamList.Count > 0 && File.Exists(characterSelectParamModPath))
                                    {
                                        //Remove old entries
                                        for (int i = 0; i < playerSettingParam_vanilla.PlayerSettingParamList.Count; i++)
                                        {
                                            if (playerSettingParam_vanilla.PlayerSettingParamList[i].CharacodeID == mod_characodeID)
                                            {
                                                RemovedPresetIds.Add(playerSettingParam_vanilla.PlayerSettingParamList[i].PSP_ID);
                                                RemovedCSPCodes.Add(playerSettingParam_vanilla.PlayerSettingParamList[i].PSP_code);
                                                playerSettingParam_vanilla.PlayerSettingParamList.RemoveAt(i);
                                                i--;
                                            }
                                        }
                                        //Add new entries
                                        for (int i = 0; i < playerSettingParam_mod.PlayerSettingParamList.Count; i++)
                                        {
                                            PlayerSettingParamModel psp_entry = (PlayerSettingParamModel)playerSettingParam_mod.PlayerSettingParamList[i].Clone();
                                            if (i == 0)
                                                charMessageID = psp_entry.CharacterNameMessageID;
                                            psp_entry.CharacodeID = mod_characodeID;
                                            psp_entry.PSP_ID = playerSettingParam_vanilla.MaxSlot() + i + 1;
                                            AddedPresetIds.Add(psp_entry.PSP_ID);
                                            if (psp_entry.ReferenceCharacodeID > characode_count)
                                            {
                                                psp_entry.ReferenceCharacodeID = mod_characodeID;
                                                psp_entry.Unk = 1;
                                            }
                                            if (psp_entry.MainPSP_ID != -1)
                                            {
                                                psp_entry.MainPSP_ID = AddedPresetIds[0];
                                            }
                                            psp_entry.DLC_ID = -1;
                                            playerSettingParam_vanilla.PlayerSettingParamList.Add(psp_entry);
                                        }
                                        IsPspModified = true;
                                    }
                                } else
                                {
                                    for (int i = 0; i < playerSettingParam_mod.PlayerSettingParamList.Count; i++)
                                    {
                                        PlayerSettingParamModel psp_entry = (PlayerSettingParamModel)playerSettingParam_mod.PlayerSettingParamList[i].Clone();
                                        psp_entry.CharacodeID = mod_characodeID;
                                        psp_entry.PSP_ID = playerSettingParam_vanilla.MaxSlot() + i + 1;
                                        if (i == 0)
                                            charMessageID = psp_entry.CharacterNameMessageID;
                                        AddedPresetIds.Add(playerSettingParam_vanilla.MaxSlot() + i + 1);
                                        if (psp_entry.ReferenceCharacodeID > characode_count)
                                        {
                                            psp_entry.ReferenceCharacodeID = mod_characodeID;
                                            psp_entry.Unk = 1;
                                        }
                                        if (psp_entry.MainPSP_ID != -1)
                                        {
                                            psp_entry.MainPSP_ID = AddedPresetIds[0];
                                        }
                                        psp_entry.DLC_ID = -1;
                                        playerSettingParam_vanilla.PlayerSettingParamList.Add(psp_entry);
                                    }
                                }
                                break;
                            case "NS4":
                                playerSettingParamS4_mod.OpenFile(playerSettingParamModPath);

                                foreach (PlayerSettingParamModel psp_entry in playerSettingParamS4_mod.PlayerSettingParamList)
                                {
                                    string costume_csp_code = psp_entry.PSP_code;
                                    int csp_code_index = 0;
                                    do
                                    {
                                        csp_code_index++;
                                        costume_csp_code = psp_entry.PSP_code + "_" + csp_code_index.ToString("D6");
                                    }
                                    while (playerSettingParam_vanilla.PSPCodeExists(costume_csp_code));

                                    csp_code_replace.Add(psp_entry.PSP_code, costume_csp_code);
                                    psp_entry.PSP_code = costume_csp_code;
                                }








                                if (replace_character)
                                {
                                    if (playerSettingParamS4_mod.PlayerSettingParamList.Count > 0 && File.Exists(characterSelectParamModPath))
                                    {
                                        //Remove old entries
                                        for (int i = 0; i < playerSettingParam_vanilla.PlayerSettingParamList.Count; i++)
                                        {
                                            if (playerSettingParam_vanilla.PlayerSettingParamList[i].CharacodeID == mod_characodeID)
                                            {
                                                RemovedPresetIds.Add(playerSettingParam_vanilla.PlayerSettingParamList[i].PSP_ID);
                                                RemovedCSPCodes.Add(playerSettingParam_vanilla.PlayerSettingParamList[i].PSP_code);
                                                playerSettingParam_vanilla.PlayerSettingParamList.RemoveAt(i);
                                                i--;
                                            }
                                        }
                                        //Add new entries
                                        for (int i = 0; i < playerSettingParamS4_mod.PlayerSettingParamList.Count; i++)
                                        {
                                            PlayerSettingParamModel psp_entry = (PlayerSettingParamModel)playerSettingParamS4_mod.PlayerSettingParamList[i].Clone();
                                            if (i == 0)
                                                charMessageID = psp_entry.CharacterNameMessageID;
                                            psp_entry.CharacodeID = mod_characodeID;
                                            psp_entry.PSP_ID = playerSettingParam_vanilla.MaxSlot() + i + 1;
                                            AddedPresetIds.Add(psp_entry.PSP_ID);
                                            if (psp_entry.ReferenceCharacodeID > characode_count)
                                            {
                                                psp_entry.ReferenceCharacodeID = mod_characodeID;
                                                psp_entry.Unk = 1;
                                            }
                                            if (psp_entry.MainPSP_ID != -1)
                                            {
                                                psp_entry.MainPSP_ID = AddedPresetIds[0];
                                            }
                                            psp_entry.DLC_ID = -1;
                                            playerSettingParam_vanilla.PlayerSettingParamList.Add(psp_entry);

                                            CostumeParamModel costume_entry = new CostumeParamModel();
                                            costume_entry.PlayerSettingParamID = psp_entry.PSP_ID;
                                            costume_entry.EntryString = costumeParam_vanilla.LastCostume();
                                            costume_entry.EntryIndex = 0; //used for unlocking
                                            costume_entry.EntryType = 0;
                                            costume_entry.UnlockCondition = 1;
                                            costume_entry.UnlockCost = 0;
                                            costumeParam_vanilla.CostumeParamList.Add(costume_entry);
                                        }
                                        IsPspModified = true;
                                    }
                                } else
                                {
                                    for (int i = 0; i < playerSettingParamS4_mod.PlayerSettingParamList.Count; i++)
                                    {
                                        PlayerSettingParamModel psp_entry = (PlayerSettingParamModel)playerSettingParamS4_mod.PlayerSettingParamList[i].Clone();
                                        psp_entry.CharacodeID = mod_characodeID;
                                        psp_entry.PSP_ID = playerSettingParam_vanilla.MaxSlot() + i + 1;
                                        if (i == 0)
                                            charMessageID = psp_entry.CharacterNameMessageID;
                                        AddedPresetIds.Add(playerSettingParam_vanilla.MaxSlot() + i + 1);
                                        if (psp_entry.ReferenceCharacodeID > characode_count)
                                        {
                                            psp_entry.ReferenceCharacodeID = mod_characodeID;
                                            psp_entry.Unk = 1;
                                        }
                                        if (psp_entry.MainPSP_ID != -1)
                                        {
                                            psp_entry.MainPSP_ID = AddedPresetIds[0];
                                        }
                                        psp_entry.DLC_ID = -1;
                                        playerSettingParam_vanilla.PlayerSettingParamList.Add(psp_entry);

                                        CostumeParamModel costume_entry = new CostumeParamModel();
                                        costume_entry.PlayerSettingParamID = psp_entry.PSP_ID;
                                        costume_entry.EntryString = costumeParam_vanilla.LastCostume();
                                        costume_entry.EntryIndex = 0; //used for unlocking
                                        costume_entry.EntryType = 0;
                                        costume_entry.UnlockCondition = 1;
                                        costume_entry.UnlockCost = 0;
                                        costumeParam_vanilla.CostumeParamList.Add(costume_entry);

                                    }
                                }
                                break;
                        }

                    }

                    //costumeBreakColorParam file
                    CostumeBreakColorParamViewModel costumeBreakColorParam_mod = new CostumeBreakColorParamViewModel();
                    CostumeBreakColorParamS4ViewModel costumeBreakColorParamS4_mod = new CostumeBreakColorParamS4ViewModel();
                    if (File.Exists(costumeBreakColorParamModPath))
                    {
                        switch (stormVersion)
                        {

                            case "NSC":
                                costumeBreakColorParam_mod.OpenFile(costumeBreakColorParamModPath);
                                if (replace_character)
                                {
                                    if (costumeBreakColorParam_mod.CostumeBreakColorParamList.Count > 0)
                                    {
                                        //Remove old entries
                                        for (int i = 0; i < costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Count; i++)
                                        {
                                            if (RemovedPresetIds.Contains(costumeBreakColorParam_vanilla.CostumeBreakColorParamList[i].PlayerSettingParamID))
                                            {
                                                costumeBreakColorParam_vanilla.CostumeBreakColorParamList.RemoveAt(i);
                                                i--;
                                            }
                                        }
                                        //Add new entries
                                        for (int i = 0; i < costumeBreakColorParam_mod.CostumeBreakColorParamList.Count; i++)
                                        {
                                            CostumeBreakColorParamModel costumeColor_entry = (CostumeBreakColorParamModel)costumeBreakColorParam_mod.CostumeBreakColorParamList[i].Clone();
                                            costumeColor_entry.PlayerSettingParamID = AddedPresetIds[i];
                                            costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Add(costumeColor_entry);
                                        }
                                    }
                                } else
                                {
                                    //Add new entries
                                    for (int i = 0; i < costumeBreakColorParam_mod.CostumeBreakColorParamList.Count; i++)
                                    {
                                        CostumeBreakColorParamModel costumeColor_entry = (CostumeBreakColorParamModel)costumeBreakColorParam_mod.CostumeBreakColorParamList[i].Clone();
                                        costumeColor_entry.PlayerSettingParamID = AddedPresetIds[i];
                                        costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Add(costumeColor_entry);
                                    }
                                }
                                break;
                            case "NS4":
                                costumeBreakColorParamS4_mod.OpenFile(costumeBreakColorParamModPath);
                                if (replace_character)
                                {
                                    if (costumeBreakColorParamS4_mod.CostumeBreakColorParamList.Count > 0)
                                    {
                                        //Remove old entries
                                        for (int i = 0; i < costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Count; i++)
                                        {
                                            if (RemovedPresetIds.Contains(costumeBreakColorParam_vanilla.CostumeBreakColorParamList[i].PlayerSettingParamID))
                                            {
                                                costumeBreakColorParam_vanilla.CostumeBreakColorParamList.RemoveAt(i);
                                                i--;
                                            }
                                        }
                                        //Add new entries
                                        for (int i = 0; i < costumeBreakColorParamS4_mod.CostumeBreakColorParamList.Count; i++)
                                        {
                                            CostumeBreakColorParamModel costumeColor_entry = (CostumeBreakColorParamModel)costumeBreakColorParamS4_mod.CostumeBreakColorParamList[i].Clone();
                                            costumeColor_entry.PlayerSettingParamID = AddedPresetIds[i];
                                            costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Add(costumeColor_entry);
                                        }
                                    }
                                } else
                                {
                                    //Add new entries
                                    for (int i = 0; i < costumeBreakColorParamS4_mod.CostumeBreakColorParamList.Count; i++)
                                    {
                                        CostumeBreakColorParamModel costumeColor_entry = (CostumeBreakColorParamModel)costumeBreakColorParamS4_mod.CostumeBreakColorParamList[i].Clone();
                                        costumeColor_entry.PlayerSettingParamID = AddedPresetIds[i];
                                        costumeBreakColorParam_vanilla.CostumeBreakColorParamList.Add(costumeColor_entry);
                                    }
                                }
                                break;
                        }

                    }
                    //costumeParam file
                    CostumeParamViewModel costumeParam_mod = new CostumeParamViewModel();
                    if (File.Exists(costumeParamModPath))
                    {
                        costumeParam_mod.OpenFile(costumeParamModPath);
                        if (replace_character)
                        {
                            if (costumeParam_mod.CostumeParamList.Count > 0 && File.Exists(characterSelectParamModPath))
                            {
                                //Remove old entries
                                for (int i = 0; i < costumeParam_vanilla.CostumeParamList.Count; i++)
                                {
                                    if (RemovedPresetIds.Contains(costumeParam_vanilla.CostumeParamList[i].PlayerSettingParamID))
                                    {
                                        costumeParam_vanilla.CostumeParamList.RemoveAt(i);
                                        i--;
                                    }
                                }
                                //Add new entries
                                int old_preset_id = 0;
                                int presetIdIndex = -1;
                                for (int i = 0; i < costumeParam_mod.CostumeParamList.Count; i++)
                                {
                                    CostumeParamModel costume_entry = (CostumeParamModel)costumeParam_mod.CostumeParamList[i].Clone();
                                    if (costume_entry.PlayerSettingParamID != old_preset_id)
                                    {
                                        presetIdIndex++;
                                        old_preset_id = costume_entry.PlayerSettingParamID;
                                    }
                                    costume_entry.PlayerSettingParamID = AddedPresetIds[presetIdIndex];
                                    costume_entry.EntryString = costumeParam_vanilla.LastCostume();
                                    //costume_entry.EntryIndex = costumeParam_vanilla.LastEntry();
                                    costume_entry.EntryIndex = 0; //used for unlocking
                                    costumeParam_vanilla.CostumeParamList.Add(costume_entry);
                                }

                            } else
                            {
                                if (RemovedPresetIds.Count == AddedPresetIds.Count && IsPspModified)
                                {
                                    for (int i = 0; i < costumeParam_vanilla.CostumeParamList.Count; i++)
                                    {
                                        if (RemovedPresetIds.Contains(costumeParam_vanilla.CostumeParamList[i].PlayerSettingParamID))
                                        {
                                            int index = RemovedPresetIds.IndexOf(costumeParam_vanilla.CostumeParamList[i].PlayerSettingParamID);
                                            costumeParam_vanilla.CostumeParamList[i].PlayerSettingParamID = AddedPresetIds[index];
                                        }
                                    }
                                } else
                                {
                                    //Remove old entries
                                    for (int i = 0; i < costumeParam_vanilla.CostumeParamList.Count; i++)
                                    {
                                        if (RemovedPresetIds.Contains(costumeParam_vanilla.CostumeParamList[i].PlayerSettingParamID))
                                        {
                                            costumeParam_vanilla.CostumeParamList.RemoveAt(i);
                                            i--;
                                        }
                                    }
                                    for (int i = 0; i < AddedPresetIds.Count; i++)
                                    {
                                        for (int c = 0; c < 2; c++)
                                        {
                                            CostumeParamModel costume_entry = new CostumeParamModel();
                                            costume_entry.PlayerSettingParamID = AddedPresetIds[i];
                                            costume_entry.EntryString = costumeParam_vanilla.LastCostume();
                                            //costume_entry.EntryIndex = costumeParam_vanilla.LastEntry();
                                            costume_entry.EntryIndex = 0; //used for unlocking
                                            costume_entry.CharacterName = charMessageID;
                                            costume_entry.UnlockCost = 0;
                                            costume_entry.UnlockCondition = 1;
                                            costume_entry.EntryType = c;
                                            costumeParam_vanilla.CostumeParamList.Add(costume_entry);
                                        }
                                    }
                                }
                            }
                        } else
                        {
                            //Add new entries
                            int old_preset_id = 0;
                            int presetIdIndex = -1;
                            for (int i = 0; i < costumeParam_mod.CostumeParamList.Count; i++)
                            {
                                CostumeParamModel costume_entry = (CostumeParamModel)costumeParam_mod.CostumeParamList[i].Clone();
                                if (costume_entry.PlayerSettingParamID != old_preset_id)
                                {
                                    presetIdIndex++;
                                    old_preset_id = costume_entry.PlayerSettingParamID;
                                }
                                costume_entry.PlayerSettingParamID = AddedPresetIds[presetIdIndex];
                                costume_entry.EntryString = costumeParam_vanilla.LastCostume();
                                //costume_entry.EntryIndex = costumeParam_vanilla.LastEntry();
                                costume_entry.EntryIndex = 0; //used for unlocking
                                costumeParam_vanilla.CostumeParamList.Add(costume_entry);
                            }
                        }
                    }

                    //skillCustomizeParam file
                    SkillCustomizeParamViewModel skillCustomizeParam_mod = new SkillCustomizeParamViewModel();
                    SkillCustomizeParamS4ViewModel skillCustomizeParamS4_mod = new SkillCustomizeParamS4ViewModel();
                    if (File.Exists(skillCustomizeParamModPath))
                    {
                        switch (stormVersion)
                        {

                            case "NSC":
                                skillCustomizeParam_mod.OpenFile(skillCustomizeParamModPath);
                                if (replace_character)
                                {
                                    for (int i = 0; i < skillCustomizeParam_vanilla.SkillCustomizeParamList.Count; i++)
                                    {
                                        if (skillCustomizeParam_vanilla.SkillCustomizeParamList[i].CharacodeID == mod_characodeID)
                                        {
                                            skillCustomizeParam_vanilla.SkillCustomizeParamList[i] = skillCustomizeParam_mod.SkillCustomizeParamList[0];
                                            break;
                                        }
                                    }
                                } else
                                {
                                    SkillCustomizeParamModel skillEntry = (SkillCustomizeParamModel)skillCustomizeParam_mod.SkillCustomizeParamList[0].Clone();
                                    skillEntry.CharacodeID = mod_characodeID;
                                    skillCustomizeParam_vanilla.SkillCustomizeParamList.Add(skillEntry);
                                }
                                break;
                            case "NS4":
                                skillCustomizeParamS4_mod.OpenFile(skillCustomizeParamModPath);
                                if (replace_character)
                                {
                                    for (int i = 0; i < skillCustomizeParam_vanilla.SkillCustomizeParamList.Count; i++)
                                    {
                                        if (skillCustomizeParam_vanilla.SkillCustomizeParamList[i].CharacodeID == mod_characodeID)
                                        {
                                            skillCustomizeParam_vanilla.SkillCustomizeParamList[i] = skillCustomizeParamS4_mod.SkillCustomizeParamList[0];
                                            break;
                                        }
                                    }
                                } else
                                {
                                    SkillCustomizeParamModel skillEntry = (SkillCustomizeParamModel)skillCustomizeParamS4_mod.SkillCustomizeParamList[0].Clone();
                                    skillEntry.CharacodeID = mod_characodeID;
                                    skillCustomizeParam_vanilla.SkillCustomizeParamList.Add(skillEntry);
                                }
                                break;
                        }
                        
                    }

                    //spSkillCustomizeParam file
                    SpSkillCustomizeParamViewModel spSkillCustomizeParam_mod = new SpSkillCustomizeParamViewModel();
                    if (File.Exists(spSkillCustomizeParamModPath))
                    {
                        spSkillCustomizeParam_mod.OpenFile(spSkillCustomizeParamModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < spSkillCustomizeParam_vanilla.SpSkillCustomizeParamList.Count; i++)
                            {
                                if (spSkillCustomizeParam_vanilla.SpSkillCustomizeParamList[i].CharacodeID == mod_characodeID)
                                {
                                    spSkillCustomizeParam_vanilla.SpSkillCustomizeParamList[i] = spSkillCustomizeParam_mod.SpSkillCustomizeParamList[0];
                                    break;
                                }
                            }
                        } else
                        {
                            SpSkillCustomizeParamModel spSkillEntry = (SpSkillCustomizeParamModel)spSkillCustomizeParam_mod.SpSkillCustomizeParamList[0].Clone();
                            spSkillEntry.CharacodeID = mod_characodeID;
                            spSkillCustomizeParam_vanilla.SpSkillCustomizeParamList.Add(spSkillEntry);
                        }
                    }

                    //skillIndexSettingParam file
                    SkillIndexSettingParamViewModel skillIndexSettingParam_mod = new SkillIndexSettingParamViewModel();
                    if (File.Exists(skillIndexSettingParamModPath))
                    {
                        skillIndexSettingParam_mod.OpenFile(skillIndexSettingParamModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < skillIndexSettingParam_vanilla.SkillIndexSettingParamList.Count; i++)
                            {
                                if (skillIndexSettingParam_vanilla.SkillIndexSettingParamList[i].CharacodeID == mod_characodeID)
                                {
                                    skillIndexSettingParam_vanilla.SkillIndexSettingParamList[i] = skillIndexSettingParam_mod.SkillIndexSettingParamList[0];
                                    break;
                                }
                            }
                        } else
                        {
                            SkillIndexSettingParamModel skillIndexEntry = (SkillIndexSettingParamModel)skillIndexSettingParam_mod.SkillIndexSettingParamList[0].Clone();
                            skillIndexEntry.CharacodeID = mod_characodeID;
                            skillIndexSettingParam_vanilla.SkillIndexSettingParamList.Add(skillIndexEntry);
                        }
                    }

                    //supportSkillRecoverySpeedParam file
                    SupportSkillRecoverySpeedParamViewModel SupportSkillRecoverySpeedParam_mod = new SupportSkillRecoverySpeedParamViewModel();
                    if (File.Exists(supportSkillRecoverySpeedParamModPath))
                    {
                        SupportSkillRecoverySpeedParam_mod.OpenFile(supportSkillRecoverySpeedParamModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < supportSkillRecoverySpeedParam_vanilla.SupportSkillRecoverySpeedParamList.Count; i++)
                            {
                                if (supportSkillRecoverySpeedParam_vanilla.SupportSkillRecoverySpeedParamList[i].CharacodeID == mod_characodeID)
                                {
                                    supportSkillRecoverySpeedParam_vanilla.SupportSkillRecoverySpeedParamList[i] = SupportSkillRecoverySpeedParam_mod.SupportSkillRecoverySpeedParamList[0];
                                    break;
                                }
                            }
                        } else
                        {
                            SupportSkillRecoverySpeedParamModel supportSkillRecoverySpeedParamEntry = (SupportSkillRecoverySpeedParamModel)SupportSkillRecoverySpeedParam_mod.SupportSkillRecoverySpeedParamList[0].Clone();
                            supportSkillRecoverySpeedParamEntry.CharacodeID = mod_characodeID;
                            supportSkillRecoverySpeedParam_vanilla.SupportSkillRecoverySpeedParamList.Add(supportSkillRecoverySpeedParamEntry);
                        }
                    }

                    //privateCamera file
                    PrivateCameraViewModel privateCamera_mod = new PrivateCameraViewModel();
                    if (File.Exists(privateCameraModPath))
                    {
                        privateCamera_mod.OpenFile(privateCameraModPath);
                        if (!character_mod.Partner)
                        {
                            if (replace_character)
                            {
                                for (int i = 0; i < privateCamera_vanilla.PrivateCameraList.Count; i++)
                                {
                                    if (privateCamera_vanilla.PrivateCameraList[i].CharacodeIndex == mod_characodeID)
                                    {
                                        privateCamera_vanilla.PrivateCameraList[i] = privateCamera_mod.PrivateCameraList[0];
                                        break;
                                    }
                                }
                            } else
                            {
                                PrivateCameraModel privateCameraEntry = (PrivateCameraModel)privateCamera_mod.PrivateCameraList[0].Clone();
                                privateCameraEntry.CharacodeIndex = mod_characodeID;
                                privateCamera_vanilla.PrivateCameraList.Add(privateCameraEntry);
                            }
                        } else
                        {
                            PrivateCameraModel privateCameraEntry = new PrivateCameraModel();
                            privateCameraEntry.CharacodeIndex = mod_characodeID;
                            privateCameraEntry.Unk1 = -1;
                            privateCameraEntry.Unk2 = -1;
                            privateCameraEntry.FOV = -1;
                            privateCameraEntry.FOV2 = -1;
                            privateCameraEntry.CameraHeight = -1;
                            privateCameraEntry.CameraHeight2 = -1;
                            privateCameraEntry.CameraAngle = -1;
                            privateCameraEntry.CameraDistance = -1;
                            privateCameraEntry.CameraDistance2 = -1;
                            privateCameraEntry.CameraMovement = -1;
                            privateCameraEntry.CameraSpeed = -1;
                            privateCamera_vanilla.PrivateCameraList.Add(privateCameraEntry);
                        }
                    } else
                    {
                        PrivateCameraModel privateCameraEntry = new PrivateCameraModel();
                        privateCameraEntry.CharacodeIndex = mod_characodeID;
                        privateCameraEntry.Unk1 = -1;
                        privateCameraEntry.Unk2 = -1;
                        privateCameraEntry.FOV = -1;
                        privateCameraEntry.FOV2 = -1;
                        privateCameraEntry.CameraHeight = -1;
                        privateCameraEntry.CameraHeight2 = -1;
                        privateCameraEntry.CameraAngle = -1;
                        privateCameraEntry.CameraDistance = -1;
                        privateCameraEntry.CameraDistance2 = -1;
                        privateCameraEntry.CameraMovement = -1;
                        privateCameraEntry.CameraSpeed = -1;
                        privateCamera_vanilla.PrivateCameraList.Add(privateCameraEntry);
                    }

                    //playerIcon file
                    PlayerIconViewModel playerIcon_mod = new PlayerIconViewModel();
                    if (File.Exists(playerIconModPath))
                    {
                        playerIcon_mod.OpenFile(playerIconModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < playerIcon_vanilla.playerIconList.Count; i++)
                            {
                                if (playerIcon_vanilla.playerIconList[i].CharacodeID == mod_characodeID)
                                {
                                    playerIcon_vanilla.playerIconList.RemoveAt(i);
                                    i--;
                                }
                            }
                        }
                        for (int i = 0; i < playerIcon_mod.playerIconList.Count; i++)
                        {
                            PlayerIconModel playerIconEntry = (PlayerIconModel)playerIcon_mod.playerIconList[i].Clone();
                            playerIconEntry.CharacodeID = mod_characodeID;
                            if (!CharselLoadedIconsList.Contains(playerIconEntry.BaseIcon) && !CharselIconNamesList.Contains(playerIconEntry.BaseIcon))
                            {
                                CharselIconNamesList.Add(playerIconEntry.BaseIcon);
                            }
                            playerIcon_vanilla.playerIconList.Add(playerIconEntry);
                        }
                    }

                    //cmnparam file
                    cmnparamViewModel cmnparam_mod = new cmnparamViewModel();
                    if (File.Exists(cmnparamModPath))
                    {
                        cmnparam_mod.OpenFile(cmnparamModPath);
                        if (replace_character)
                        {
                            for (int i = 0; i < cmnparam_vanilla.PlayerSndList.Count; i++)
                            {
                                if (cmnparam_vanilla.PlayerSndList[i].PlayerCharacode == mod_characode)
                                {
                                    cmnparam_vanilla.PlayerSndList[i] = cmnparam_mod.PlayerSndList[0];
                                    break;
                                }
                            }
                        } else
                        {
                            player_sndModel playerSndEntry = (player_sndModel)cmnparam_mod.PlayerSndList[0].Clone();
                            cmnparam_vanilla.PlayerSndList.Add(playerSndEntry);
                        }
                    }

                    //characterSelectParam file
                    CharacterSelectParamViewModel characterSelectParam_mod = new CharacterSelectParamViewModel();
                    CharacterSelectParamS4ViewModel characterSelectParamS4_mod = new CharacterSelectParamS4ViewModel();
                    if (File.Exists(characterSelectParamModPath))
                    {
                        int page = -1;
                        int slot = -1;
                        switch (stormVersion)
                        {

                            case "NSC":
                                characterSelectParam_mod.OpenFile(characterSelectParamModPath);




                                if (replace_character)
                                {
                                    if (!character_mod.EnableRosterChange)
                                    {
                                        for (int i = 0; i < characterSelectParam_vanilla.CharacterSelectParamList.Count; i++)
                                        {
                                            if (RemovedCSPCodes.Contains(characterSelectParam_vanilla.CharacterSelectParamList[i].CSP_code))
                                            {
                                                if (page == -1)
                                                {
                                                    page = characterSelectParam_vanilla.CharacterSelectParamList[i].PageIndex;
                                                    slot = characterSelectParam_vanilla.CharacterSelectParamList[i].SlotIndex;
                                                }
                                            }
                                        }
                                        for (int i = 0; i < characterSelectParam_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParam_mod.CharacterSelectParamList[i].Clone();
                                            csp_entry.PageIndex = page;
                                            csp_entry.SlotIndex = slot;
                                            csp_entry.CostumeIndex = i;
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.SaveInFile = true;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    } else
                                    {
                                        for (int i = 0; i < characterSelectParam_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParam_mod.CharacterSelectParamList[i].Clone();

                                            int cfgPage = -1;
                                            int cfgSlot = -1;
                                            int cfgCostume = -1;
                                            if (TryReadCSPConfig(character_mod.RootPath, csp_entry.CSP_code, out int pRead, out int sRead, out int cRead))
                                            {
                                                cfgPage = pRead;
                                                cfgSlot = sRead;
                                                cfgCostume = cRead;
                                            }
                                            if (cfgPage == -1)
                                            {
                                                page = characterSelectParam_vanilla.MaxPage();
                                                slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                                if (slot == 25)
                                                {
                                                    page++;
                                                    slot = 1;
                                                }
                                                cfgPage = page;
                                                cfgSlot = slot;
                                                cfgCostume = i;
                                            }

                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.PageIndex = cfgPage;
                                            csp_entry.SlotIndex = cfgSlot;
                                            csp_entry.CostumeIndex = cfgCostume;
                                            csp_entry.SaveInFile = true;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    }
                                } else
                                {

                                    if (!character_mod.EnableRosterChange)
                                    {
                                        page = characterSelectParam_vanilla.MaxPage();
                                        slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                        if (slot == 25)
                                        {
                                            page++;
                                            slot = 1;
                                        }
                                        for (int i = 0; i < characterSelectParam_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParam_mod.CharacterSelectParamList[i].Clone();
                                            csp_entry.PageIndex = page;
                                            csp_entry.SlotIndex = slot;
                                            csp_entry.CostumeIndex = i;
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.SaveInFile = true;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    } else
                                    {
                                        page = characterSelectParam_vanilla.MaxPage();
                                        slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                        if (slot == 25)
                                        {
                                            page++;
                                            slot = 1;
                                        }
                                        for (int i = 0; i < characterSelectParam_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParam_mod.CharacterSelectParamList[i].Clone();

                                            int cfgPage = -1;
                                            int cfgSlot = -1;
                                            int cfgCostume = -1;

                                            if (TryReadCSPConfig(character_mod.RootPath, csp_entry.CSP_code, out int pRead, out int sRead, out int cRead))
                                            {
                                                cfgPage = pRead;
                                                cfgSlot = sRead;
                                                cfgCostume = cRead;
                                            }
                                            if (cfgPage == -1)
                                            {

                                                cfgPage = page;
                                                cfgSlot = slot;
                                                cfgCostume = i;
                                            }

                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.PageIndex = cfgPage;
                                            csp_entry.SlotIndex = cfgSlot;
                                            csp_entry.CostumeIndex = cfgCostume;
                                            csp_entry.SaveInFile = true;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    }




                                }
                                break;
                            case "NS4":
                                characterSelectParamS4_mod.OpenFile(characterSelectParamModPath);

                                if (replace_character)
                                {
                                    if (!character_mod.EnableRosterChange)
                                    {
                                        for (int i = 0; i < characterSelectParam_vanilla.CharacterSelectParamList.Count; i++)
                                        {
                                            if (RemovedCSPCodes.Contains(characterSelectParam_vanilla.CharacterSelectParamList[i].CSP_code))
                                            {
                                                if (page == -1)
                                                {
                                                    page = characterSelectParam_vanilla.CharacterSelectParamList[i].PageIndex;
                                                    slot = characterSelectParam_vanilla.CharacterSelectParamList[i].SlotIndex;
                                                }
                                            }
                                        }

                                        for (int i = 0; i < characterSelectParamS4_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParamS4_mod.CharacterSelectParamList[i].Clone();
                                            csp_entry.PageIndex = page;
                                            csp_entry.SlotIndex = slot;
                                            csp_entry.CostumeIndex = i;
                                            csp_entry.SaveInFile = true;
                                            csp_entry.DictionaryCode = "";
                                            csp_entry.DictionaryIndex = -1;
                                            csp_entry.Unk = 1;
                                            csp_entry.CostumeName = "practice_normal";
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.CharselValues.P1_customization_pos_x = (float)-76.1235122680664;
                                            csp_entry.CharselValues.P1_customization_pos_y = (float)73.89142608642578;
                                            csp_entry.CharselValues.P1_customization_pos_z = (float)-323.99603271484375;
                                            csp_entry.CharselValues.P1_customization_rot = (float)14.025724411010742;
                                            csp_entry.CharselValues.P1_customization_light_x = (float)18.649999618530273;
                                            csp_entry.CharselValues.P1_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P1_customization_light_z = (float)0.38999998569488525;
                                            csp_entry.CharselValues.P2_customization_pos_x = (float)76.17376708984375;
                                            csp_entry.CharselValues.P2_customization_pos_y = (float)360.3885498046875;
                                            csp_entry.CharselValues.P2_customization_pos_z = (float)-285.6630859375;
                                            csp_entry.CharselValues.P2_customization_rot = (float)345.3846130371094;
                                            csp_entry.CharselValues.P2_customization_light_x = (float)11.158173561096191;
                                            csp_entry.CharselValues.P2_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P2_customization_light_z = (float)-16.35211753845215;

                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    } else
                                    {
                                        page = characterSelectParam_vanilla.MaxPage();
                                        slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                        if (slot == 25)
                                        {
                                            page++;
                                            slot = 1;
                                        }
                                        for (int i = 0; i < characterSelectParamS4_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParamS4_mod.CharacterSelectParamList[i].Clone();

                                            int cfgPage = -1;
                                            int cfgSlot = -1;
                                            int cfgCostume = -1;
                                            if (TryReadCSPConfig(character_mod.RootPath, csp_entry.CSP_code, out int pRead, out int sRead, out int cRead))
                                            {
                                                cfgPage = pRead;
                                                cfgSlot = sRead;
                                                cfgCostume = cRead;
                                            }
                                            if (cfgPage == -1)
                                            {
                                                cfgPage = page;
                                                cfgSlot = slot;
                                                cfgCostume = i;
                                            }

                                            csp_entry.PageIndex = cfgPage;
                                            csp_entry.SlotIndex = cfgSlot;
                                            csp_entry.CostumeIndex = cfgCostume >= 0 ? cfgCostume : i;
                                            csp_entry.SaveInFile = true;
                                            //Debug.WriteLine($"{csp_entry.CSP_code} was replaced S4!");
                                            csp_entry.DictionaryCode = "";
                                            csp_entry.DictionaryIndex = -1;
                                            csp_entry.Unk = 1;
                                            csp_entry.CostumeName = "practice_normal";
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            csp_entry.CharselValues.P1_customization_pos_x = (float)-76.1235122680664;
                                            csp_entry.CharselValues.P1_customization_pos_y = (float)73.89142608642578;
                                            csp_entry.CharselValues.P1_customization_pos_z = (float)-323.99603271484375;
                                            csp_entry.CharselValues.P1_customization_rot = (float)14.025724411010742;
                                            csp_entry.CharselValues.P1_customization_light_x = (float)18.649999618530273;
                                            csp_entry.CharselValues.P1_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P1_customization_light_z = (float)0.38999998569488525;
                                            csp_entry.CharselValues.P2_customization_pos_x = (float)76.17376708984375;
                                            csp_entry.CharselValues.P2_customization_pos_y = (float)360.3885498046875;
                                            csp_entry.CharselValues.P2_customization_pos_z = (float)-285.6630859375;
                                            csp_entry.CharselValues.P2_customization_rot = (float)345.3846130371094;
                                            csp_entry.CharselValues.P2_customization_light_x = (float)11.158173561096191;
                                            csp_entry.CharselValues.P2_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P2_customization_light_z = (float)-16.35211753845215;

                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    }
                                } else
                                {
                                    if (!character_mod.EnableRosterChange)
                                    {
                                        page = character_mod.Page;
                                        slot = character_mod.Slot;
                                        if (character_mod.Page == -1)
                                        {
                                            page = characterSelectParam_vanilla.MaxPage();
                                            slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                            if (slot == 25)
                                            {
                                                page++;
                                                slot = 1;
                                            }
                                        }
                                        for (int i = 0; i < characterSelectParamS4_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParamS4_mod.CharacterSelectParamList[i].Clone();
                                            csp_entry.PageIndex = page;
                                            csp_entry.SlotIndex = slot;
                                            csp_entry.CostumeIndex = i;
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            Debug.WriteLine($"{csp_entry.CSP_code} was added S4!");
                                            csp_entry.DictionaryCode = "";
                                            csp_entry.DictionaryIndex = -1;
                                            csp_entry.Unk = 1;
                                            csp_entry.CostumeName = "practice_normal";
                                            csp_entry.SaveInFile = true;
                                            csp_entry.CharselValues.P1_customization_pos_x = (float)-76.1235122680664;
                                            csp_entry.CharselValues.P1_customization_pos_y = (float)73.89142608642578;
                                            csp_entry.CharselValues.P1_customization_pos_z = (float)-323.99603271484375;
                                            csp_entry.CharselValues.P1_customization_rot = (float)14.025724411010742;
                                            csp_entry.CharselValues.P1_customization_light_x = (float)18.649999618530273;
                                            csp_entry.CharselValues.P1_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P1_customization_light_z = (float)0.38999998569488525;
                                            csp_entry.CharselValues.P2_customization_pos_x = (float)76.17376708984375;
                                            csp_entry.CharselValues.P2_customization_pos_y = (float)360.3885498046875;
                                            csp_entry.CharselValues.P2_customization_pos_z = (float)-285.6630859375;
                                            csp_entry.CharselValues.P2_customization_rot = (float)345.3846130371094;
                                            csp_entry.CharselValues.P2_customization_light_x = (float)11.158173561096191;
                                            csp_entry.CharselValues.P2_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P2_customization_light_z = (float)-16.35211753845215;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    } else
                                    {
                                        page = character_mod.Page;
                                        slot = character_mod.Slot;
                                        if (character_mod.Page == -1)
                                        {
                                            page = characterSelectParam_vanilla.MaxPage();
                                            slot = characterSelectParam_vanilla.FreeSlotOnPage(page);
                                            if (slot == 25)
                                            {
                                                page++;
                                                slot = 1;
                                            }
                                        }
                                        for (int i = 0; i < characterSelectParamS4_mod.CharacterSelectParamList.Count; i++)
                                        {
                                            CharacterSelectParamModel csp_entry = (CharacterSelectParamModel)characterSelectParamS4_mod.CharacterSelectParamList[i].Clone();

                                            int cfgPage = -1;
                                            int cfgSlot = -1;
                                            int cfgCostume = -1;
                                            if (TryReadCSPConfig(character_mod.RootPath, csp_entry.CSP_code, out int pRead, out int sRead, out int cRead))
                                            {
                                                cfgPage = pRead;
                                                cfgSlot = sRead;
                                                cfgCostume = cRead;
                                            }
                                            if (cfgPage == -1)
                                            {
                                                cfgPage = page;
                                                cfgSlot = slot;
                                                cfgCostume = i;
                                            }
                                            csp_entry.PageIndex = cfgPage;
                                            csp_entry.SlotIndex = cfgSlot;
                                            csp_entry.CostumeIndex = cfgCostume;
                                            if (csp_code_replace.ContainsKey(csp_entry.CSP_code))
                                            {
                                                csp_entry.CSP_code = csp_code_replace[csp_entry.CSP_code];
                                            }
                                            Debug.WriteLine($"{csp_entry.CSP_code} was added S4!");
                                            csp_entry.DictionaryCode = "";
                                            csp_entry.DictionaryIndex = -1;
                                            csp_entry.Unk = 1;
                                            csp_entry.CostumeName = "practice_normal";
                                            csp_entry.SaveInFile = true;
                                            csp_entry.CharselValues.P1_customization_pos_x = (float)-76.1235122680664;
                                            csp_entry.CharselValues.P1_customization_pos_y = (float)73.89142608642578;
                                            csp_entry.CharselValues.P1_customization_pos_z = (float)-323.99603271484375;
                                            csp_entry.CharselValues.P1_customization_rot = (float)14.025724411010742;
                                            csp_entry.CharselValues.P1_customization_light_x = (float)18.649999618530273;
                                            csp_entry.CharselValues.P1_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P1_customization_light_z = (float)0.38999998569488525;
                                            csp_entry.CharselValues.P2_customization_pos_x = (float)76.17376708984375;
                                            csp_entry.CharselValues.P2_customization_pos_y = (float)360.3885498046875;
                                            csp_entry.CharselValues.P2_customization_pos_z = (float)-285.6630859375;
                                            csp_entry.CharselValues.P2_customization_rot = (float)345.3846130371094;
                                            csp_entry.CharselValues.P2_customization_light_x = (float)11.158173561096191;
                                            csp_entry.CharselValues.P2_customization_light_y = (float)68.86000061035156;
                                            csp_entry.CharselValues.P2_customization_light_z = (float)-16.35211753845215;
                                            characterSelectParam_vanilla.CharacterSelectParamList.Add(csp_entry);
                                        }
                                    }
                                }
                                break;
                        }
                        
                    }


                    //supportActionParam file
                    SupportActionParamViewModel supportActionParam_mod = new SupportActionParamViewModel();
                    if (File.Exists(supportActionParamModPath))
                    {
                        supportActionParam_mod.OpenFile(supportActionParamModPath);
                        for (int i = 0; i < supportActionParam_vanilla.SupportActionParamList.Count; i++)
                        {
                            if (supportActionParam_vanilla.SupportActionParamList[i].CharacodeID == mod_characodeID)
                            {
                                supportActionParam_vanilla.SupportActionParamList[i] = supportActionParam_mod.SupportActionParamList[0];
                                break;
                            }
                        }
                        SupportActionParamModel supportActionParamEntry = (SupportActionParamModel)supportActionParam_mod.SupportActionParamList[0].Clone();
                        supportActionParamEntry.CharacodeID = mod_characodeID;
                        supportActionParam_vanilla.SupportActionParamList.Add(supportActionParamEntry);
                    }

                    /*---------------------------------------NOT REQUIRED FILES-------------------------------------------*/
                    //costumeBreakParam file
                    CostumeBreakParamViewModel costumeBreakParam_mod = new CostumeBreakParamViewModel();
                    if (File.Exists(costumeBreakParamModPath))
                    {
                        costumeBreakParam_mod.OpenFile(costumeBreakParamModPath);
                        //Remove old entries
                        for (int i = 0; i < costumeBreakParam_vanilla.CostumeBreakParamList.Count; i++)
                        {
                            if (costumeBreakParam_vanilla.CostumeBreakParamList[i].CharacodeID == mod_characodeID)
                            {
                                costumeBreakParam_vanilla.CostumeBreakParamList.RemoveAt(i);
                                i--;
                            }
                        }
                        //Add new entries
                        for (int i = 0; i < costumeBreakParam_mod.CostumeBreakParamList.Count; i++)
                        {
                            CostumeBreakParamModel costumeColor_entry = (CostumeBreakParamModel)costumeBreakParam_mod.CostumeBreakParamList[i].Clone();
                            costumeColor_entry.CharacodeID = mod_characodeID;
                            costumeBreakParam_vanilla.CostumeBreakParamList.Add(costumeColor_entry);
                        }
                    }

                    //AwakeAura file
                    AwakeAuraViewModel awakeeAura_mod = new AwakeAuraViewModel();
                    if (File.Exists(awakeAuraModPath))
                    {
                        awakeeAura_mod.OpenFile(awakeAuraModPath);
                        for (int i = 0; i < awakeAura_vanilla.AwakeAuraList.Count; i++)
                        {
                            if (awakeAura_vanilla.AwakeAuraList[i].Characode == mod_characode)
                            {
                                awakeAura_vanilla.AwakeAuraList.RemoveAt(i);
                                i--;
                            }
                        }
                        for (int i = 0; i < awakeeAura_mod.AwakeAuraList.Count; i++)
                        {
                            awakeAura_vanilla.AwakeAuraList.Add((AwakeAuraModel)awakeeAura_mod.AwakeAuraList[i].Clone());
                        }
                    }
                    //AppearanceAnm file
                    AppearanceAnmViewModel appearanceAnm_mod = new AppearanceAnmViewModel();
                    if (File.Exists(appearanceAnmModPath))
                    {
                        appearanceAnm_mod.OpenFile(appearanceAnmModPath);
                        for (int i = 0; i < appearanceAnm_vanilla.AppearanceAnmList.Count; i++)
                        {
                            if (appearanceAnm_vanilla.AppearanceAnmList[i].CharacodeID == mod_characodeID)
                            {
                                appearanceAnm_vanilla.AppearanceAnmList.RemoveAt(i);
                                i--;
                            }
                        }
                        for (int i = 0; i < appearanceAnm_mod.AppearanceAnmList.Count; i++)
                        {
                            AppearanceAnmModel appearanceAnmEntry = (AppearanceAnmModel)appearanceAnm_mod.AppearanceAnmList[i].Clone();
                            appearanceAnmEntry.CharacodeID = mod_characodeID;
                            appearanceAnm_vanilla.AppearanceAnmList.Add(appearanceAnmEntry);
                        }
                    }
                    //afterAttachObject file
                    AfterAttachObjectViewModel afterAttachObject_mod = new AfterAttachObjectViewModel();
                    if (File.Exists(afterAttachObjectModPath))
                    {
                        afterAttachObject_mod.OpenFile(afterAttachObjectModPath);
                        for (int i = 0; i < afterAttachObject_vanilla.AfterAttachObjectList.Count; i++)
                        {
                            if (baseModel.Contains(afterAttachObject_vanilla.AfterAttachObjectList[i].Characode)
                                || awakeModel.Contains(afterAttachObject_vanilla.AfterAttachObjectList[i].Characode)
                                || afterAttachObject_vanilla.AfterAttachObjectList[i].Costume == mod_characode)
                            {
                                afterAttachObject_vanilla.AfterAttachObjectList.RemoveAt(i);
                                i--;
                            }
                        }
                        for (int i = 0; i < afterAttachObject_mod.AfterAttachObjectList.Count; i++)
                        {
                            afterAttachObject_vanilla.AfterAttachObjectList.Add((AfterAttachObjectModel)afterAttachObject_mod.AfterAttachObjectList[i].Clone());
                        }
                    }
                    //playerDoubleEffectParam file
                    PlayerDoubleEffectParamViewModel playerDoubleEffectParam_mod = new PlayerDoubleEffectParamViewModel();
                    if (File.Exists(playerDoubleEffectParamModPath))
                    {
                        playerDoubleEffectParam_mod.OpenFile(playerDoubleEffectParamModPath);
                        for (int i = 0; i < playerDoubleEffectParam_vanilla.PlayerDoubleEffectParamList.Count; i++)
                        {
                            if (playerDoubleEffectParam_vanilla.PlayerDoubleEffectParamList[i].CharacodeID == mod_characodeID)
                            {
                                playerDoubleEffectParam_vanilla.PlayerDoubleEffectParamList.RemoveAt(i);
                                i--;
                            }
                        }
                        for (int i = 0; i < playerDoubleEffectParam_mod.PlayerDoubleEffectParamList.Count; i++)
                        {
                            PlayerDoubleEffectParamModel playerDoubleEffectEntry = (PlayerDoubleEffectParamModel)playerDoubleEffectParam_mod.PlayerDoubleEffectParamList[i].Clone();
                            playerDoubleEffectEntry.CharacodeID = mod_characodeID;
                            playerDoubleEffectParam_vanilla.PlayerDoubleEffectParamList.Add(playerDoubleEffectEntry);
                        }
                    }
                    //spTypeSupportParam file
                    SpTypeSupportParamViewModel spTypeSupportParam_mod = new SpTypeSupportParamViewModel();
                    if (File.Exists(spTypeSupportParamModPath))
                    {
                        spTypeSupportParam_mod.OpenFile(spTypeSupportParamModPath);
                        for (int i = 0; i < spTypeSupportParam_vanilla.SpTypeSupportParamList.Count; i++)
                        {
                            if (spTypeSupportParam_vanilla.SpTypeSupportParamList[i].CharacodeID == mod_characodeID)
                            {
                                spTypeSupportParam_vanilla.SpTypeSupportParamList.RemoveAt(i);
                                break;
                            }
                        }
                        SpTypeSupportParamModel spTypeSupportParamEntry = (SpTypeSupportParamModel)spTypeSupportParam_mod.SpTypeSupportParamList[0].Clone();
                        spTypeSupportParamEntry.CharacodeID = mod_characodeID;
                        spTypeSupportParam_vanilla.SpTypeSupportParamList.Add(spTypeSupportParamEntry);
                    }

                    //specialCondParam file
                    byte[] specialCondParam_mod = Array.Empty<byte>();
                    if (File.Exists(specialCondParamModPath))
                    {
                        specialCondParam_mod = File.ReadAllBytes(specialCondParamModPath);
                        if (specialCondParam_mod.Length % 32 != 0)
                            result.Warnings.Add($"character {mod_characode}: specialCondParam size {specialCondParam_mod.Length} is not a multiple of 32 bytes");
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: specialCondParam ({specialCondParam_mod.Length} bytes)");

                        // Each entry is 0x20 bytes. Desktop 2.1.1.0 patched only the
                        // first entry; Android patches every complete record so community
                        // mods containing more than one special condition remain valid.
                        for (int p = 0; p + 0x20 <= specialCondParam_mod.Length; p += 0x20)
                        {
                            string conditionName = BinaryReader.b_ReadString(specialCondParam_mod, p);
                            specialCondParam_mod = BinaryReader.b_ReplaceBytes(specialCondParam_mod, new byte[4], p + 0x17);
                            specialCondParam_mod = BinaryReader.b_ReplaceBytes(specialCondParam_mod, BitConverter.GetBytes(mod_characodeID), p + 0x18);
                            if (!string.IsNullOrWhiteSpace(conditionName))
                                apiExpectations.SpecialConditions.Add((conditionName, mod_characodeID));
                        }
                        specialCondParam_vanilla = BinaryReader.b_AddBytes(specialCondParam_vanilla, specialCondParam_mod);
                    }

                    //specialCondParam file
                    byte[] partnerSlotParam_mod = new byte[0];
                    if (File.Exists(partnerSlotParamModPath))
                    {
                        partnerSlotParam_mod = File.ReadAllBytes(partnerSlotParamModPath);
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: partnerSlotParam ({partnerSlotParam_mod.Length} bytes)");
                        for (int p = 0; p + 0x20 <= partnerSlotParam_mod.Length; p += 0x20)
                        {
                            partnerSlotParam_mod = BinaryReader.b_ReplaceBytes(partnerSlotParam_mod, new byte[4], p + 0x17);
                            partnerSlotParam_mod = BinaryReader.b_ReplaceBytes(partnerSlotParam_mod, BitConverter.GetBytes(mod_characodeID), p + 0x18);
                        }
                        partnerSlotParam_vanilla = BinaryReader.b_AddBytes(partnerSlotParam_vanilla, partnerSlotParam_mod);
                    }

                    //susanooCondParam file
                    byte[] susanooCondParam_mod = new byte[0];
                    if (File.Exists(susanooCondParamModPath))
                    {
                        susanooCondParam_mod = File.ReadAllBytes(susanooCondParamModPath);
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: susanooCondParam ({susanooCondParam_mod.Length} bytes)");
                        for (int p = 0; p + 0x20 <= susanooCondParam_mod.Length; p += 0x20)
                        {
                            susanooCondParam_mod = BinaryReader.b_ReplaceBytes(susanooCondParam_mod, new byte[4], p + 0x17);
                            susanooCondParam_mod = BinaryReader.b_ReplaceBytes(susanooCondParam_mod, BitConverter.GetBytes(mod_characodeID), p + 0x18);
                        }
                        susanooCondParam_vanilla = BinaryReader.b_AddBytes(susanooCondParam_vanilla, susanooCondParam_mod);
                    }

                    //guardEffectParam file
                    GuardEffectParamViewModel guardEffectParam_mod = new GuardEffectParamViewModel();
                    if (File.Exists(guardEffectParamModPath))
                    {
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: guardEffectParam");
                        guardEffectParam_mod.OpenFile(guardEffectParamModPath);
                        for (int i = 0; i < guardEffectParam_mod.GuardEffectParamList.Count; i++)
                        {

                            GuardEffectParamModel guardEffectParamEntry = (GuardEffectParamModel)guardEffectParam_mod.GuardEffectParamList[i].Clone();
                            guardEffectParamEntry.CharacodeID = mod_characodeID;
                            guardEffectParam_vanilla.GuardEffectParamList.Add(guardEffectParamEntry);
                        }
                    }
                    //ougiAwakeningParam file
                    byte[] ougiAwakeningParam_mod = new byte[0];
                    if (File.Exists(ougiAwakeningParamModPath))
                    {
                        ougiAwakeningParam_mod = File.ReadAllBytes(ougiAwakeningParamModPath);
                        if (ougiAwakeningParam_mod.Length % 4 != 0) result.Warnings.Add($"character {mod_characode}: ougiAwakeningParam size {ougiAwakeningParam_mod.Length} is not a multiple of 4 bytes");
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: ougiAwakeningParam ({ougiAwakeningParam_mod.Length} bytes)");
                        ougiAwakeningParam_mod = BinaryReader.b_ReplaceBytes(ougiAwakeningParam_mod, BitConverter.GetBytes(mod_characodeID), 0, 0, 4);
                        apiExpectations.OugiAwakeningIds.Add(mod_characodeID);
                        ougiAwakeningParam_vanilla = BinaryReader.b_AddBytes(ougiAwakeningParam_vanilla, ougiAwakeningParam_mod);
                    }

                    byte[] gudoBallParam_mod = new byte[0];
                    if (File.Exists(gudoBallParamModPath))
                    {
                        gudoBallParam_mod = File.ReadAllBytes(gudoBallParamModPath);
                        result.SpecialApiFilesMerged++;
                        result.FeatureDetails.Add($"character {mod_characode}: gudoBallParam ({gudoBallParam_mod.Length} bytes)");
                        gudoBallParam_mod = BinaryReader.b_ReplaceBytes(gudoBallParam_mod, BitConverter.GetBytes(mod_characodeID), 0, 0, 4);
                        gudoBallParam_vanilla = BinaryReader.b_AddBytes(gudoBallParam_vanilla, gudoBallParam_mod);
                    }


                    if (Directory.Exists(messageInfoModPath) && messageState is not null)
                    {
                        if (MessageInfoMerger.QueueDirectory(messageState, messageInfoModPath, stormVersion, $"character {mod_characode}"))
                            messageInfoModified = true;
                    }

                    //damageprm file
                    DamagePrmViewModel damageprm_mod = new DamagePrmViewModel();
                    if (File.Exists(damageprmModPath))
                    {
                        damageprm_mod.OpenFile(damageprmModPath);
                        for (int i = 0; i < damageprm_mod.DamagePrmList.Count; i++)
                        {
                            var entry = (DamagePrmModel)damageprm_mod.DamagePrmList[i].Clone();

                            // если мод для Storm 4 — читаем int32 по смещению 0x6C, ищем соответствие в списках и заменяем
                            if (stormVersion == "NS4" && entry.Data != null && entry.Data.Length >= 0x6C + 4)
                            {
                                int oldIndex = BitConverter.ToInt32(entry.Data, 0x6C);
                                if (oldIndex >= 0 && Program.CONDITION_NS4_LIST != null && oldIndex < Program.CONDITION_NS4_LIST.Length)
                                {
                                    string cond = Program.CONDITION_NS4_LIST[oldIndex];
                                    if (!string.IsNullOrEmpty(cond) && Program.CONDITION_NSC_LIST != null)
                                    {
                                        int newIndex = Array.IndexOf(Program.CONDITION_NSC_LIST, cond);
                                        if (newIndex >= 0)
                                        {
                                            byte[] newBytes = BitConverter.GetBytes(newIndex);
                                            Array.Copy(newBytes, 0, entry.Data, 0x6C, 4);
                                        }
                                        // если newIndex < 0 — соответствия нет, оставляем старый индекс
                                    }
                                }
                                // если oldIndex вне диапазона — не трогаем
                            }

                            damageprm_vanilla.DamagePrmList.Add(entry);
                        }
                    }

                    //prm
                    PRMEditorViewModel prm_mod = new PRMEditorViewModel();

                    var modDir = new DirectoryInfo(Path.GetDirectoryName(Path.GetDirectoryName(character_mod.RootPath)));
                    var prmFiles = modDir
                        .GetFiles($"{mod_characode}prm.bin.xfbin", SearchOption.AllDirectories)
                        .OrderBy(f => f.DirectoryName, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (prmFiles.Length > 0)
                    {
                        result.PrmFilesDetected += prmFiles.Length;
                        if (prmFiles.Length > 1) result.Warnings.Add($"character {mod_characode}: multiple PRM candidates detected ({prmFiles.Length}); desktop-compatible last path wins");
                        string prm_path = prmFiles.Last().FullName;
                        result.FeatureDetails.Add($"character {mod_characode}: PRM candidate {Path.GetFileName(prm_path)}");
                        string relative = RelativeFromData(prm_path);
                        string new_prm_path = Path.Combine(root_folder, "param_files", relative);

                        // Только если оба файла существуют, выполняем merge
                        if (File.Exists(prm_path) && File.Exists(damageeffModPath))
                        {
                            // load mod and vanilla view‑models
                            var damageeff_mod = new DamageEffViewModel(); damageeff_mod.OpenFile(damageeffModPath);
                            var effectprm_mod = new EffectPrmViewModel();

                            var effectIdMap = new Dictionary<int, int>();
                            if (File.Exists(effectprmModPath))
                            {
                                effectprm_mod.OpenFile(effectprmModPath);
                                foreach (var modEntry in effectprm_mod.EffectPrmList)
                                {
                                    int newId = effectprm_vanilla.MaxEffectID() + 1;
                                    effectIdMap[modEntry.EffectPrmID] = newId;
                                    Debug.WriteLine($"Effect Entry, old id = {modEntry.EffectPrmID}, new id = {newId}");
                                    modEntry.EffectPrmID = newId;
                                    effectprm_vanilla.EffectPrmList.Add((EffectPrmModel)modEntry.Clone());
                                }
                            }

                            // remap EffectPrmID in damageEff_mod and build hit‑ID map
                            var hitIdMap = new Dictionary<int, int>();
                            foreach (var de in damageeff_mod.DamageEffList)
                            {
                                if (effectIdMap.TryGetValue(de.EffectPrmID, out var mapped))
                                {
                                    de.EffectPrmID = mapped;
                                    de.ExtraEffectPrmID = 0;
                                }
                                int newHit = damageeff_vanilla.MaxDamageID() + 1;
                                hitIdMap[de.DamageEffID] = newHit;

                                var clone = (DamageEffModel)de.Clone();
                                Debug.WriteLine($"Damage Entry, old id = {clone.DamageEffID}, new id = {newHit}");
                                clone.DamageEffID = newHit;
                                if (hitIdMap.TryGetValue(clone.ExtraDamageEffID, out var extra))
                                    clone.ExtraDamageEffID = extra;

                                damageeff_vanilla.DamageEffList.Add(clone);
                            }

                            // open and correct prm
                            prm_mod.OpenFile(prm_path);
                            foreach (var ver in prm_mod.VerList)
                                foreach (var sec in ver.PL_ANM_Sections)
                                    foreach (var fn in sec.FunctionList)
                                    {
                                        if (hitIdMap.TryGetValue(fn.DamageHitEffectID, out var nid))
                                            fn.DamageHitEffectID = (short)nid;

                                    }

                            // save result
                            Directory.CreateDirectory(Path.GetDirectoryName(new_prm_path)!);
                            prm_mod.SaveFileAs(new_prm_path);
                            result.PrmFilesRemapped++;
                            result.FeatureDetails.Add($"character {mod_characode}: PRM damage-effect IDs remapped");
                        }
                        else if (File.Exists(prm_path))
                        {
                            result.FeatureDetails.Add($"character {mod_characode}: PRM passthrough (no character damageeff.bin.xfbin to remap)");
                        }
                    }




                }

                foreach (StageModModel stage_mod in StageList)
                {

                    string stormVersion = stage_mod.GameVersion;
                    string messageInfoModPath = Path.Combine(stage_mod.RootPath, "data", "message");
                    string stageInfoModPath = Path.Combine(stage_mod.RootPath, "data", "stage", "StageInfo.bin.xfbin");

                    string mod_stagename = stage_mod.StageName;
                    int mod_stageID = -1;
                    int BGM_ID = Convert.ToInt32(stage_mod.BgmID);
                    bool replace_stage = false;

                    //Read StageInfo file and find entry
                    for (int i = 0; i < stageInfo_vanilla.StageInfoList.Count; i++)
                    {
                        if (stageInfo_vanilla.StageInfoList[i].StageName == mod_stagename)
                        {
                            mod_stageID = i;
                            replace_stage = true;
                            break;
                        }
                    }
                    StageInfoViewModel stageInfo_mod = new StageInfoViewModel();
                    if (File.Exists(stageInfoModPath))
                    {
                        stageInfo_mod.OpenFile(stageInfoModPath);
                        stageInfoModified = true;
                        // Assume xmlStageIDs is an ObservableCollection<string> containing stageIDs from the XML.
                        var xmlStageIDs = new ObservableCollection<string>
                                {
                            "STAGE_SI00A", "STAGE_SD30A", "STAGE_SD14A", "STAGE_SD01D", "STAGE_SD01B",
                            "STAGE_SD03B", "STAGE_SD03E", "STAGE_SD03A", "STAGE_SD03D", "STAGE_SD18A",
                            "STAGE_SD04B", "STAGE_SD04C", "STAGE_SD05C", "STAGE_SD05A", "STAGE_SD05D",
                            "STAGE_SD05B", "STAGE_SD31A", "STAGE_SI43A", "STAGE_SD00B", "STAGE_SI01A",
                            "STAGE_SD08A", "STAGE_SD06A", "STAGE_SI02A", "STAGE_SD07A", "STAGE_SD07B",
                            "STAGE_SI06A", "STAGE_SD33A", "STAGE_SD10A", "STAGE_SI09A_NR", "STAGE_SI08A",
                            "STAGE_SD32A", "STAGE_SD16A", "STAGE_SD11A", "STAGE_SD12A", "STAGE_SI10A",
                            "STAGE_SI10B", "STAGE_SD24A", "STAGE_SD13A", "STAGE_SD15A_NOSNOW", "STAGE_SD17A",
                            "STAGE_SD17B", "STAGE_SD22A", "STAGE_SD22B", "STAGE_SD25A", "STAGE_SD19A",
                            "STAGE_SD23A", "STAGE_SD21A", "STAGE_SD26A", "STAGE_SI33A", "STAGE_SI35A",
                            "STAGE_SI42B", "STAGE_SI42A", "STAGE_SI44A", "STAGE_SD60A", "STAGE_SI45A",
                            "STAGE_SI50E", "STAGE_SI51C", "STAGE_SD62B", "STAGE_SD62A", "STAGE_SD70B",
                            "STAGE_SI70A", "STAGE_SI71A", "STAGE_SI80A", "STAGE_SI81A", "STAGE_SI81B",
                            "STAGE_SD51A", "STAGE_0_MAID_IN_HEAVEN"
                                                    };

                        if (replace_stage)
                        {
                            // Check if the stage already exists in the XML list.
                            string stageName = stageInfo_mod.StageInfoList[0].StageName;
                            if (!xmlStageIDs.Contains(stageName))
                            {
                                StagesToAdd.Add(stage_mod);
                            }
                            stageInfo_vanilla.StageInfoList[mod_stageID] = (StageInfoModel)stageInfo_mod.StageInfoList[0].Clone();
                        } else
                        {
                            StagesToAdd.Add(stage_mod);
                            stageInfo_vanilla.StageInfoList.Add((StageInfoModel)stageInfo_mod.StageInfoList[0].Clone());
                        }
                    }
                    if (Directory.Exists(messageInfoModPath) && messageState is not null)
                    {
                        if (MessageInfoMerger.QueueDirectory(messageState, messageInfoModPath, stormVersion, $"stage {mod_stagename}"))
                            messageInfoModified = true;
                    }
                }

        progress("Semantic params: generating character/stage selection UI...");
        LegacyUiCompiler.Output uiOutput = LegacyUiCompiler.Build(
            baseNsc, apiParam, root_folder, param_modmanager_path, generatedApiParam,
            characterSelectParam_vanilla, CharselIconNamesList, StagesToAdd, stageInfoModified, result);
        result.CharacterUiFilesGenerated = uiOutput.CharacterUiFiles;
        result.StageUiFilesGenerated = uiOutput.StageUiFiles;
        result.StageResourceXfbinsGenerated = uiOutput.StageResourceXfbins;
        result.FixedRuntimeFilesGenerated = uiOutput.FixedOverlayFiles;
        result.BaseResourceFilesStaged = uiOutput.BaseResourceFiles;

        progress("Semantic params: serializing merged XFBINs...");
        Directory.CreateDirectory(Path.Combine(param_modmanager_path, "data", "spc"));
        Directory.CreateDirectory(Path.Combine(param_modmanager_path, "data", "rpg", "param"));
        Directory.CreateDirectory(Path.Combine(param_modmanager_path, "data", "ui", "max", "select"));
        Directory.CreateDirectory(Path.Combine(param_modmanager_path, "data", "stage"));
        Directory.CreateDirectory(Path.Combine(param_modmanager_path, "data", "sound"));

        int saved = 0;
        SaveChecked(() => characode_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "characode.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "characode.bin.xfbin"), ref saved);
        SaveChecked(() => duelPlayerParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "duelPlayerParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "duelPlayerParam.xfbin"), ref saved);
        SaveChecked(() => playerSettingParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "playerSettingParam.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "playerSettingParam.bin.xfbin"), ref saved);
        SaveChecked(() => skillCustomizeParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "skillCustomizeParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "skillCustomizeParam.xfbin"), ref saved);
        SaveChecked(() => spSkillCustomizeParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "spSkillCustomizeParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "spSkillCustomizeParam.xfbin"), ref saved);
        SaveChecked(() => skillIndexSettingParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "skillIndexSettingParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "skillIndexSettingParam.xfbin"), ref saved);
        SaveChecked(() => supportSkillRecoverySpeedParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "supportSkillRecoverySpeedParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "supportSkillRecoverySpeedParam.xfbin"), ref saved);
        SaveChecked(() => privateCamera_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "privateCamera.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "privateCamera.bin.xfbin"), ref saved);
        SaveChecked(() => characterSelectParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "ui", "max", "select", "characterSelectParam.xfbin")), Path.Combine(param_modmanager_path, "data", "ui", "max", "select", "characterSelectParam.xfbin"), ref saved);
        SaveChecked(() => costumeBreakParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "costumeBreakParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "costumeBreakParam.xfbin"), ref saved);
        SaveChecked(() => costumeBreakColorParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "costumeBreakColorParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "costumeBreakColorParam.xfbin"), ref saved);
        SaveChecked(() => costumeParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "rpg", "param", "costumeParam.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "rpg", "param", "costumeParam.bin.xfbin"), ref saved);
        SaveChecked(() => playerIcon_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "player_icon.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "player_icon.xfbin"), ref saved);
        SaveChecked(() => cmnparam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "sound", "cmnparam.xfbin")), Path.Combine(param_modmanager_path, "data", "sound", "cmnparam.xfbin"), ref saved);
        SaveChecked(() => supportActionParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "supportActionParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "supportActionParam.xfbin"), ref saved);
        SaveChecked(() => awakeAura_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "awakeAura.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "awakeAura.xfbin"), ref saved);
        SaveChecked(() => appearanceAnm_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "appearanceAnm.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "appearanceAnm.xfbin"), ref saved);
        SaveChecked(() => afterAttachObject_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "afterAttachObject.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "afterAttachObject.xfbin"), ref saved);
        SaveChecked(() => playerDoubleEffectParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "playerDoubleEffectParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "playerDoubleEffectParam.xfbin"), ref saved);
        SaveChecked(() => spTypeSupportParam_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "spTypeSupportParam.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "spTypeSupportParam.xfbin"), ref saved);
        SaveChecked(() => conditionprm_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "conditionprm.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "conditionprm.bin.xfbin"), ref saved);
        SaveChecked(() => damageeff_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "damageeff.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "damageeff.bin.xfbin"), ref saved);
        SaveChecked(() => effectprm_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "effectprm.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "effectprm.bin.xfbin"), ref saved);
        SaveChecked(() => damageprm_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "spc", "damageprm.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "spc", "damageprm.bin.xfbin"), ref saved);
        if (stageInfoModified) SaveChecked(() => stageInfo_vanilla.SaveFileAs(Path.Combine(param_modmanager_path, "data", "stage", "StageInfo.bin.xfbin")), Path.Combine(param_modmanager_path, "data", "stage", "StageInfo.bin.xfbin"), ref saved);

        if (messageInfoModified && messageState is not null)
        {
            progress("Semantic params: writing localization XFBINs...");
            var msg = MessageInfoMerger.Save(messageState, param_modmanager_path);
            result.MessageSourceFilesDetected = msg.SourceFilesDetected;
            result.MessageTargetLanguagesMerged = msg.TargetLanguageMerges;
            result.MessageEntriesMerged = msg.EntriesAppended;
            result.MessageOutputsGenerated = msg.OutputsGenerated;
            result.FeatureDetails.AddRange(msg.Details);
            if (msg.MissingSourceMappings > 0)
                result.Warnings.Add($"Localization merge had {msg.MissingSourceMappings} missing language mapping(s); vanilla text was preserved for those targets. See feature details.");
        }

        conditionprmManager_vanilla.SaveFileAs(Path.Combine(generatedApiParam, "conditionprmManager.xfbin"));
        guardEffectParam_vanilla.SaveFileAs(Path.Combine(generatedApiParam, "guardEffectParam.xfbin"));
        File.WriteAllBytes(Path.Combine(generatedApiParam, "specialCondParam.xfbin"), specialCondParam_vanilla);
        File.WriteAllBytes(Path.Combine(generatedApiParam, "partnerSlotParam.xfbin"), partnerSlotParam_vanilla);
        File.WriteAllBytes(Path.Combine(generatedApiParam, "susanooCondParam.xfbin"), susanooCondParam_vanilla);
        File.WriteAllBytes(Path.Combine(generatedApiParam, "gudoBallParam.xfbin"), gudoBallParam_vanilla);
        File.WriteAllBytes(Path.Combine(generatedApiParam, "ougiAwakeningParam.xfbin"), ougiAwakeningParam_vanilla);

        SpecialApiVerifier.Verify(generatedApiParam, apiExpectations, result);

        result.ParameterXfbinsMerged = saved;
        return new Output(
            param_modmanager_path, generatedApiParam, uiOutput.GameOverlayDirectory,
            CharacterList.Count, StageList.Count, saved,
            uiOutput.CharacterUiFiles, uiOutput.StageUiFiles, uiOutput.StageResourceXfbins,
            uiOutput.FixedOverlayFiles, uiOutput.BaseResourceFiles);
    }

    private static void SaveChecked(Action saveAction, string path, ref int count)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        saveAction();
        if (!File.Exists(path) || new FileInfo(path).Length < 32)
            throw new InvalidDataException("Merged XFBIN serialization failed: " + path);
        count++;
    }

    private static bool TryReadCSPConfig(string rootPath, string cspCode, out int page, out int slot, out int costume)
    {
        page = slot = costume = -1;
        string cfgPath = Path.Combine(rootPath, "character_config.ini");
        if (!File.Exists(cfgPath) || string.IsNullOrEmpty(cspCode)) return false;
        var ini = new IniFile(cfgPath);
        bool found = false;
        if (int.TryParse(ini.Read("Page", cspCode), out int p)) { page = p; found = true; }
        if (int.TryParse(ini.Read("Slot", cspCode), out int s)) { slot = s; found = true; }
        if (int.TryParse(ini.Read("Costume", cspCode), out int c)) { costume = c; found = true; }
        return found;
    }

    private static string RelativeFromData(string fullPath)
    {
        string norm = fullPath.Replace('\\', '/');
        int idx = norm.IndexOf("/data/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            if (norm.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) return norm.Replace('/', Path.DirectorySeparatorChar);
            throw new InvalidDataException("Could not locate data/ root for PRM: " + fullPath);
        }
        return norm[(idx + 1)..].Replace('/', Path.DirectorySeparatorChar);
    }
}
