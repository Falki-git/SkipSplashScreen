using BepInEx;
using HarmonyLib;
using SpaceWarp;
using SpaceWarp.API.Mods;
using KSP.Game;
using KSP.Game.Flow;

namespace SkipSplashScreen;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class SkipSplashScreenPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    public void Start()
    {
        Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin));
    }

    public void Update()
    {
        var gameState = GameManager.Instance?.Game?.GlobalGameState?.GetState();

        if (gameState == null)
            return;

        if (GameManager.Instance?.Game?.GlobalGameState?.GetState() == GameState.MainMenu)
            Destroy(this);
    }

    [HarmonyPatch(typeof(SequentialFlow), "AddAction"), HarmonyPrefix]
    private static bool SequentialFlow_AddAction(FlowAction action)
    {
        if (action.Name == "Creating Splash Screens Prefab")
        {
            GameManager.Instance.HasPhotosensitivityWarningBeenShown = true;
            return false;
        }
        else
            return true;
    }
}