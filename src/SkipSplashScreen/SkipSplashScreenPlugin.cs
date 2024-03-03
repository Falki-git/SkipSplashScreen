using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.Game.Flow;
using KSP.Game;
using SpaceWarp;
using SpaceWarp.API.Mods;
using BepInEx.Logging;
using UnityEngine;

namespace SkipSplashScreen;

[BepInPlugin("com.github.falki.skip_splash_screen", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class SkipSplashScreenPlugin : BaseSpaceWarpPlugin
{
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    private new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SkipSplashScreen");

    private GameObject _mainMenu;
    private CampaignMenu _campaignMenuScript;
    private bool _singlePlayerMenuTriggered;
    private bool _loadInitiated;
    private bool _hasFinished;

    private ConfigEntry<bool> _loadLastSavedCampaign;
    private ConfigEntry<bool> _loadIgnoreAutoSaves;

    public void Start()
    {
        Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin));

        _loadLastSavedCampaign = Config.Bind(
            MyPluginInfo.PLUGIN_NAME,
            "Auto load last played campaign",
            false,
            "Automatically loads the last save game file after main menu is finished loading.");

        _loadIgnoreAutoSaves = Config.Bind(
            MyPluginInfo.PLUGIN_NAME,
            "Ignore auto-saves when loading last save game",
            false,
            "If enabled, auto-saves are ignored when automatically loading last save game.");

    }

    public void Update()
    {
        if (_hasFinished)
            return;
        
        var gameState = GameManager.Instance?.Game?.GlobalGameState?.GetState();

        if (gameState == null)
            return;

        if (gameState == GameState.MainMenu)
        {
            if (_loadLastSavedCampaign?.Value ?? false)
            {
                if (!_singlePlayerMenuTriggered)
                {
                    // Trigger the Single Player menu in order for the CampaignEntryTiles to get created 
                    TriggerSinglePlayerMenu();
                }
                
                if (!_loadInitiated) 
                    LoadLastSinglePlayerGame();    
            }
            else
            {
                DestroyPlugin();
            }
        }
    }

    [HarmonyPatch(typeof(SequentialFlow), "AddAction"), HarmonyPrefix]
    private static bool SequentialFlow_AddAction(FlowAction action)
    {
        if (action.Name == "Creating Splash Screens Prefab")
        {
            Logger.LogInfo("'Creating Splash Screens Prefab' action found. Skipping!");
            GameManager.Instance.HasPhotosensitivityWarningBeenShown = true;
            return false;
        }

        return true;
    }

    private void TriggerSinglePlayerMenu()
    {
        Logger.LogInfo("'Auto load last played campaign' is enabled. To turn it off go into Settings -> Mods -> Skip Splash Screen");
        
        _mainMenu = GameObject.Find(
            "GameManager/Default Game Instance(Clone)/UI Manager(Clone)/Main Canvas/MainMenu(Clone)/");
        var campaignMenu = _mainMenu.GetChild("CampaignMenu");
        _campaignMenuScript = campaignMenu.GetComponent<CampaignMenu>();

        var campaignSavesList = _campaignMenuScript.Game.SaveLoadManager.GetCampaignSaveFiles(CampaignType.SinglePlayer);
        _campaignMenuScript.FillCampaignScrollView(campaignSavesList, _campaignMenuScript._campaignScrollViewContentLastPlayedDate);

        _singlePlayerMenuTriggered = true;
    }

    private void LoadLastSinglePlayerGame()
    {
        // Wait for all the saves to load and get displayed
        // In 0.2.1 the first save of the first campaign is auto-selected when opening the menu
        if (_campaignMenuScript._campaignLoadMenu.CurrentSelectedFilePath is null)
            return;

        var save_components = _mainMenu.GetComponentsInChildren<SaveLoadDialogFileEntry>();
        Logger.LogDebug($"save_components.Length: {save_components.Length}");

        var saveGamesList = _mainMenu.GetChild("SaveGamesList");
        if (saveGamesList.transform.childCount != save_components.Length)
        {
            // Haven't seen this happen, but just in case
            Logger.LogError($"Visual ({saveGamesList.transform.childCount}) and logical {save_components.Length} save counts don't match");
            return;
        }

        for (var i = 0; i<saveGamesList.transform.childCount; ++i)
        {
            string curr_save_name = save_components[i]._labelSaveName.text;
            if (_loadIgnoreAutoSaves.Value && curr_save_name.StartsWith("autosave")) continue;
            Logger.LogInfo($"Auto loading save '{curr_save_name}'.");

            // It's called "lastPlayed" but it's actually just "lastSelected"
            // (this is remembered after closing the menu, but not after restarting the game)
            save_components[i].SetCurrentToggleState(lastPlayed: true);

            break;
        }

        _campaignMenuScript._campaignLoadMenu.LoadSelectedFile();
        DestroyPlugin();

        _loadInitiated = true;
    }

    private void DestroyPlugin()
    {
        //Logger.LogDebug($"disappears into oblivion...");
        //Destroy(this);
        
        // We'll keep the plugin alive because it's needed for config changes
        Logger.LogDebug($"{MyPluginInfo.PLUGIN_NAME} workflow completed.");
        _hasFinished = true;
    }
}