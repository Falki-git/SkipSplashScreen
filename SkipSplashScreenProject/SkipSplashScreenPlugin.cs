using BepInEx;
using HarmonyLib;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Assets;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Game;
using SpaceWarp.API.Game.Extensions;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using UnityEngine;
using KSP.Game;
using KSP.Game.Flow;
using static GUIUtil;
using UnityEngine.UIElements.UIR;
using BepInEx.Logging;
using KSP.Game.StartupFlow;

namespace SkipSplashScreen;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class SkipSplashScreenPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;
    
    private bool _isWindowOpen;
    private Rect _windowRect;

    private const string ToolbarFlightButtonID = "BTN-SkipSplashScreenFlight";
    private const string ToolbarOABButtonID = "BTN-SkipSplashScreenOAB";

    public static SkipSplashScreenPlugin Instance { get; set; }

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        // Register Flight AppBar button
        Appbar.RegisterAppButton(
            "SkipSplashScreen",
            ToolbarFlightButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );

        // Register OAB AppBar Button
        Appbar.RegisterOABAppButton(
            "SkipSplashScreen",
            ToolbarOABButtonID,
            AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
            isOpen =>
            {
                _isWindowOpen = isOpen;
                GameObject.Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
            }
        );
       
        // Fetch a configuration value or create a default one if it does not exist
        var defaultValue = "my_value";
        var configValue = Config.Bind<string>("Settings section", "Option 1", defaultValue, "Option description");
        
        // Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        Logger.LogInfo($"Option 1: {configValue.Value}");

        //Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin).Assembly);
    }

    public void Update()
    {
        if (GameManager.Instance.Game.GlobalGameState.GetState() == GameState.MainMenu)
            Destroy(this);
    }

    private static FlowAction lastFlowAction = null;
    private static List<FlowAction> flowActions = new List<FlowAction>();

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private static void FillWindow(int windowID)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("GameManager.Instance: ");
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{GameManager.Instance}");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("GameManager.Instance.Game.GlobalGameState: ");
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{GameManager.Instance.Game.GlobalGameState}");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("GameManager.Instance.Game.GlobalGameState.GetState: ");
        GUILayout.FlexibleSpace();
        GUILayout.Label($"{GameManager.Instance.Game.GlobalGameState.GetState()}");
        GUILayout.EndHorizontal();

        //GUILayout.Label($"{}");


        GameManager instance = GameManager.Instance;
        FlowAction flowAction;
        if (instance == null)
        {
            flowAction = null;
        }
        else
        {
            SequentialFlow loadingFlow = instance.LoadingFlow;
            flowAction = loadingFlow?.GetCurrentAction();            

            if (flowAction != lastFlowAction)
            {
                GUILayout.Label("Flow changed");
                lastFlowAction = flowAction;
                flowActions.Add(lastFlowAction);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("FlowAction name: ");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{flowAction.Name}");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("FlowAction Timeout: ");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{flowAction.Timeout}");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("FlowAction Description: ");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{flowAction.Description}");
            GUILayout.EndHorizontal();
            
        }
        if (flowAction == null || flowAction.Name != "Creating Splash Screens Prefab")
        {
            return;
        }
        flowAction.Timeout = 0;
        //this.showNotice = true;

        //GameObject.Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
        var splashScreenManager = GameObject.FindObjectOfType(typeof(SplashScreensManager)); // .Find(ToolbarOABButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(isOpen);
        SplashScreensManager man = new SplashScreensManager();

        



        GUI.DragWindow(new Rect(0, 0, Screen.width, Screen.height));
    }

    public void Start()
    {
        Harmony.CreateAndPatchAll(typeof(SkipSplashScreenPlugin).Assembly);
    }
}

[HarmonyPatch(typeof(SplashScreensManager))]
[HarmonyPatch("StartAnimations")]
public class SplashScreensManagerPatch
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SkipSplashScreenPlugin.SplashScreensManagerPatch");

    private static void Prefix(SplashScreensManager __instance)
    {
        Logger.LogInfo("StartAnimations harmony patch hit");
        //__instance.ResolveSplashScreens();
    }
}

[HarmonyPatch(typeof(SequentialFlow))]
public class SplashScreensManagerPatch2
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SkipSplashScreenPlugin.SplashScreensManagerPatch2");

    [HarmonyPatch("AddAction")]
    [HarmonyPrefix]
    private static bool AddActionPrefix(FlowAction action)
    {
        Logger.LogInfo($"SequentialFlow harmony patch hit. Action name: {action.Name}");
        if (action.Name == "Creating Splash Screens Prefab" || action.Name == "Parsing Loading Screens" || action.Name == "Set Loading Optimizations")
        {
            GameManager.Instance.HasPhotosensitivityWarningBeenShown = true;
            return false;
        }
            
        else
            return true;
        //__instance.ResolveSplashScreens();
    }
}

[HarmonyPatch(typeof(LandingHUD))]
public class SplashScreensManagerPatch3
{
    private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("SkipSplashScreenPlugin.LandingHUD");

    [HarmonyPatch("Start")]
    [HarmonyPrefix]
    private static bool StartPrefix(LandingHUD __instance)
    {
        Logger.LogInfo($"LandingHUD harmony patch hit.");

        //GameManager.Instance.HasPhotosensitivityWarningBeenShown = true;
        
        return true;
        //__instance.ResolveSplashScreens();
    }
}
