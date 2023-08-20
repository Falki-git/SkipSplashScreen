﻿using BepInEx;
using HarmonyLib;
using JetBrains.Annotations;
using KSP.Game.Flow;
using KSP.Game;
using SpaceWarp;
using SpaceWarp.API.Mods;

namespace SkipSplashScreen;

[BepInPlugin("com.github.falki.skip_splash_screen", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class SkipSplashScreenPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    [PublicAPI] public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    [PublicAPI] public const string ModName = MyPluginInfo.PLUGIN_NAME;
    [PublicAPI] public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    public void Start()
    {
        Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin));
    }

    public void Update()
    {
        var gameState = GameManager.Instance?.Game?.GlobalGameState?.GetState();

        if (gameState == null)
            return;

        if (gameState == GameState.MainMenu)
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
