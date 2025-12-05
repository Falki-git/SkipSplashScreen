using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.Game;
using KSP.Game.Flow;
using PatchManager.Core.Flow;
using Redux.ExtraModTypes;
using ReduxLib.Configuration;
using UnityEngine;

namespace SkipSplashScreen
{
    [HarmonyPatch]
    public class SkipSplashScreenPlugin : KerbalMod
    {
        [PublicAPI] public static SkipSplashScreenPlugin Instance { get; set; }

        [PublicAPI] public const string ModGuid = "SkipSplashScreen";
        [PublicAPI] public const string ModName = "Skip Splash Screen";
        [PublicAPI] public const string ModVer = "1.3.1";

        private GameObject _mainMenu;
        private CampaignMenu _campaignMenuScript;
        private bool _singlePlayerMenuTriggered;
        private bool _loadInitiated;
        private bool _hasFinished;

        private ConfigValue<bool> _loadLastSavedCampaign;
        private ConfigValue<bool> _loadIgnoreAutoSaves;

        public void Start()
        {
            Instance = this;
            
            CreateHarmonyAndPatchAll();

            _loadLastSavedCampaign = new (SWConfiguration.Bind(
                ModName, 
                "Auto load last played campaign", 
                false,
                "Automatically loads the last save game file after main menu is finished loading."));
            
            _loadIgnoreAutoSaves = new (SWConfiguration.Bind(
                ModName, 
                "Ignore auto-saves when loading last save game", 
                false,
                "If enabled, auto-saves are ignored when automatically loading last save game."));
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

        [HarmonyPatch(typeof(FlowManager), "AddActionsToFlow"), HarmonyPrefix]
        private static bool FlowManager_AddActionsToFlow(SequentialFlow loadingFlow)
        {
            var actionToSkip = "Creating Splash Screens Prefab";
            
            var removeCount = loadingFlow.FlowActions.RemoveAll(action => action.Name == actionToSkip);

            if (removeCount > 0)
            {
                Instance.SWLogger.LogInfo($"'{actionToSkip}' successfully removed from FlowActions.");
            }
            else
            {
                Instance.SWLogger.LogInfo($"'{actionToSkip}' not found in FlowActions.");
            }
            
            GameManager.Instance.HasPhotosensitivityWarningBeenShown = true;

            return true;
        }
        
        private void TriggerSinglePlayerMenu()
        {
            SWLogger.LogInfo("'Auto load last played campaign' is enabled. To turn it off go into Settings -> Mods -> Skip Splash Screen");

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
            SWLogger.LogDebug($"save_components.Length: {save_components.Length}");

            var saveGamesList = _mainMenu.GetChild("SaveGamesList");
            if (saveGamesList.transform.childCount != save_components.Length)
            {
                // Haven't seen this happen, but just in case
                SWLogger.LogError($"Visual ({saveGamesList.transform.childCount}) and logical {save_components.Length} save counts don't match");
                return;
            }

            for (var i = 0; i < saveGamesList.transform.childCount; ++i)
            {
                //var currentSaveName = save_components[i]._labelSaveName.text;
                FieldInfo labelField = save_components[i].GetType().GetField("_labelSaveName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (labelField == null)
                {
                    SWLogger.LogError($"No field '_labelSaveName' found in save_components[{i}]. Skipping.");
                    continue;
                }

                var label = labelField.GetValue(save_components[i]);
                PropertyInfo labelTextField = label.GetType().GetProperty("text");
                if (labelTextField == null)
                {
                    SWLogger.LogError($"No property 'text' found in save_components[{i}]._labelSaveName. Skipping.");
                    continue;
                }

                var currentSaveName = labelTextField.GetValue(label) as string;

                if (_loadIgnoreAutoSaves.Value && currentSaveName.ToLowerInvariant().StartsWith("autosave"))
                {
                    SWLogger.LogInfo($"'Ignore auto-saves' is enabled. Skipping '{currentSaveName}'.");
                    continue;
                }
                
                SWLogger.LogInfo($"Auto loading save '{currentSaveName}'.");

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
            SWLogger.LogDebug($"{SkipSplashScreenPlugin.ModGuid} workflow completed.");
            _hasFinished = true;
        }
    }
}