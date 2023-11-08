using System.Collections;
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

    private CampaignMenu _campaignMenuScript;
    private bool _singlePlayerMenuTriggered;
    private bool _loadInitiated;
    private bool _hasFinished;

    private ConfigEntry<bool> _loadLastSavedCampaign;

    public void Start()
    {
        Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin));
        
        _loadLastSavedCampaign = Config.Bind(
            MyPluginInfo.PLUGIN_NAME,
            "Auto load last played campaign",
            false,
            "Automatically loads the last save game file after main menu is finished loading.");
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
        
        var mainMenu = GameObject.Find(
            "GameManager/Default Game Instance(Clone)/UI Manager(Clone)/Main Canvas/MainMenu(Clone)/");
        var campaignMenu = mainMenu.GetChild("CampaignMenu");
        _campaignMenuScript = campaignMenu.GetComponent<CampaignMenu>();

        var campaignSavesList = _campaignMenuScript.Game.SaveLoadManager.GetCampaignSaveFiles(CampaignType.SinglePlayer);
        _campaignMenuScript.FillCampaignScrollView(campaignSavesList, _campaignMenuScript._campaignScrollViewContentLastPlayedDate);

        _singlePlayerMenuTriggered = true;
    }

    private void LoadLastSinglePlayerGame()
    {
        // Wait for the CampaignEntryTiles to get created
        if (_campaignMenuScript._campaignEntryTiles.Count == 0)
            return;
        
        CampaignTileEntry latestCampaign = null;
        DateTime latestPlayed = DateTime.MinValue;

        // Determine what campaign was played last.
        foreach (var campaign in _campaignMenuScript._campaignEntryTiles)
        {
            var lastPlayed = DateTime.Parse(campaign.CampaignLastPlayedTime);

            if (latestCampaign == null || lastPlayed > latestPlayed)
            {
                latestCampaign = campaign;
                latestPlayed = lastPlayed;
            }
        }

        if (latestCampaign != null)
        {
            // What campaign tile entry is clicked, last saved game is automatically selected
            latestCampaign.OnCampaignClick();
            Logger.LogInfo($"Auto loading campaign '{latestCampaign.CampaignName}'.");

            StartCoroutine(Load());
            _loadInitiated = true;
        }
    }

    private IEnumerator Load()
    {
        // Wait for the next frame cause save file won't be still selected here
        yield return null;
        _campaignMenuScript._campaignLoadMenu.LoadSelectedFile();
        
        DestroyPlugin();
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