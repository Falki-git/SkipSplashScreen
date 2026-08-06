# Mod specifics — Skip Splash Screen (Redux)

The single mod developed in this repo. Read this before doing feature work; keep
`CLAUDE.md` mod-agnostic and put anything mod-specific here.

## What it does

Skips KSP2's boot splash-screen sequence and the photosensitivity/health warning, so the game
goes straight toward the main menu. Optionally it can then auto-load the last-played
single-player campaign. It is a **development convenience mod** — the README explicitly frames
it as "to be used only for faster iteration when developing mods".

Author: **Falki**. Upstream: <https://github.com/Falki-git/SkipSplashScreen>.

## Identity & layout

| Thing | Value |
|---|---|
| Mod folder | `Assets/SkipSplashScreen/` |
| Entry point | `Assets/SkipSplashScreen/Code/SkipSplashScreenPlugin.cs` |
| Namespace / asmdef / assembly | `SkipSplashScreen` → `SkipSplashScreen.dll` |
| Base class | `Redux.ExtraModTypes.KerbalMod` (MonoBehaviour — the mod needs `Update`) |
| `mod_id` | `SkipSplashScreen` (was `com.github.falki.skip_splash_screen` pre-Redux) |
| Version | `1.3.1` (`ModVer` const, `swinfo.asset`, `swinfo.json` — keep all three in sync) |
| KSP2 range | min `0.2.3`, max `*` |
| Dependencies | `SpaceWarp2 >= 2.0.0` only |
| Metadata source of truth | `swinfo.asset` (Unity asset); `swinfo.json` is the emitted/committed copy used by the version check |
| Version-check URL | raw `swinfo.json` on branch **`redux/master`** — repoint it if that branch is ever renamed; see [Branching](#branching) |

There is **no `Copied/` content** (no localizations, no PatchManager Lua patches) and no
`Definitions/` — this mod is pure code. `Assets/SkipSplashScreen/Copied.meta` exists but the
folder is empty.

## How it works

Two independent mechanisms, both in the single `SkipSplashScreenPlugin` class:

**1. Splash skip — a Harmony prefix on the loading flow.**
```csharp
[HarmonyPatch(typeof(FlowManager), "AddActionsToFlow"), HarmonyPrefix]
private static bool FlowManager_AddActionsToFlow(SequentialFlow loadingFlow)
```
It removes every flow action named `"Creating Splash Screens Prefab"` from
`loadingFlow.FlowActions`, sets `GameManager.Instance.HasPhotosensitivityWarningBeenShown = true`,
and returns `true` (lets the original run).

- `FlowManager` here is **`PatchManager.Core.Flow.FlowManager`** — PatchManager is compiled
  *into* `Assembly-CSharp.dll` in Redux, so the type resolves from the game assembly.
  `SequentialFlow` is `KSP.Game.Flow.SequentialFlow`.
- This is the main Redux-era behavioural change: the pre-Redux version prefixed
  `SequentialFlow.AddAction` and returned `false` to veto the single action. The Redux version
  instead lets the flow be built and strips the action afterwards, from PatchManager's hook.
- Patches are applied via `CreateHarmonyAndPatchAll()` in `Start()`.

**2. Auto-load last campaign — an `Update()` state machine.** Gated on the
`Auto load last played campaign` config value. Once `GlobalGameState.GetState() == GameState.MainMenu`:
- `TriggerSinglePlayerMenu()` — finds the main menu by hardcoded hierarchy path
  `GameManager/Default Game Instance(Clone)/UI Manager(Clone)/Main Canvas/MainMenu(Clone)/`,
  grabs the `CampaignMenu` component, and calls `FillCampaignScrollView(...)` so the save
  entries actually get instantiated (they don't exist until the Single Player menu is opened).
- `LoadLastSinglePlayerGame()` — waits until `_campaignLoadMenu.CurrentSelectedFilePath` is
  non-null, walks the `SaveLoadDialogFileEntry` components, optionally skips entries whose name
  starts with `autosave`, calls `SetCurrentToggleState(lastPlayed: true)` on the first
  acceptable one, then `_campaignLoadMenu.LoadSelectedFile()`.
- `DestroyPlugin()` doesn't actually destroy anything — it just sets `_hasFinished = true` so
  `Update()` short-circuits. The instance is deliberately kept alive so the in-game config UI
  still has something to bind to.

Note the comment in the code: `_isLastPlayed`/`lastPlayed` is really "last *selected*" — it
persists across closing the menu but not across a game restart.

## Configuration

Bound in `Start()` via `SWConfiguration.Bind(...)`, section `"Skip Splash Screen"` (the
`ModName` constant), wrapped in `ConfigValue<bool>`:

| Key | Default | Effect |
|---|---|---|
| `Auto load last played campaign` | `false` | Auto-loads the last save after the main menu finishes loading |
| `Ignore auto-saves when loading last save game` | `false` | Skips saves whose name starts with `autosave` when picking |

In-game path: **Settings → Mods → Skip Splash Screen**. The editor-test config file lives at
`Assets/Mods/__Testing/SkipSplashScreen/SkipSplashScreen-config.json`.

With `Auto load last played campaign` off (the default) only the Harmony patch does anything —
`Update()` calls `DestroyPlugin()` on the first `MainMenu` tick and goes quiet.

## Game members it depends on (fragile surface)

These are the exact game internals that break when KSP2 updates — check them first when the mod
stops working:

- `PatchManager.Core.Flow.FlowManager.AddActionsToFlow(SequentialFlow)` — `internal static`.
- The flow-action name string `"Creating Splash Screens Prefab"`.
- `GameManager.Instance.HasPhotosensitivityWarningBeenShown`.
- The hardcoded main-menu `GameObject.Find` path, and the `"CampaignMenu"` / `"SaveGamesList"`
  child names.
- `KSP.Game.CampaignMenu`: `FillCampaignScrollView`, `_campaignScrollViewContentLastPlayedDate`,
  `_campaignLoadMenu` — **all `private` in the shipped assembly** (verified by decompiling
  `Packages/KSP2_x64/Assembly-CSharp.dll`). `CampaignMenu.Game` is public (inherited from
  `KerbalMonoBehaviour`), as is `CampaignLoadMenu.LoadSelectedFile`.
- `KSP.Game.SaveLoadDialogFileEntry`: `SetCurrentToggleState` is public; `_labelSaveName` is
  private and is **already** read via reflection (with graceful `LogError` + `continue` on miss).

> ⚠️ **Known blocker.** The checked-in `Assembly-CSharp.dll` is *not* publicized, yet the
> auto-load path still references those three private `CampaignMenu` members directly. Per
> `CLAUDE.md` the publicizer is broken and must not be run, so a fresh compile of this file will
> fail on them — they need converting to reflection, the same way `_labelSaveName` already was.
> The last known-good build (see `redux.log`, 2025-12-06) predates this state.

## Logging

The code currently uses the inherited **`SWLogger`** (`SWLogger.LogInfo/LogDebug/LogError`), and
the static Harmony patch reaches it through `Instance.SWLogger`. `CLAUDE.md` prescribes
per-class ReduxLib loggers instead — treat the existing calls as a deliberate deviation to be
migrated if the file is reworked, not as the pattern to copy into new classes. Log lines appear
as `[... : SkipSplashScreen]` in `redux.log` / `Player.log`.

`Instance` is a public static set in `Start()` purely so the static Harmony prefix can log.

## Build & deploy

ThunderKit pipelines under `Assets/SkipSplashScreen/Pipelines/` — the user runs these in the
Unity Editor; Claude cannot:

| Pipeline | What it produces |
|---|---|
| `Build for Editor` | Stages into `Assets/Mods/__Testing/SkipSplashScreen/` for in-editor play testing |
| `Build for Player` | Builds against the player install |
| `Deploy to Zip File` | Stages `Deploy/SkipSplashScreen/` (dll + `swinfo.json`), zips to `Deploy/SkipSplashScreen.zip` — this is the release artifact |

Release build flag is on (`releaseBuild: 1`) in the deploy pipeline.

## Branching

This repo uses the **`redux/*`** branch model, adopted 2026-08-06. `CLAUDE.md`'s git section was
rewritten to match, so the two agree; what follows is the repo-specific detail behind it.

| Branch | Role |
|---|---|
| `redux/master` | Master-like branch for the Redux port — holds the **currently released** Redux version. Nothing lands here except a finished release (normally by merging `redux/development`). Never commit directly. |
| `redux/development` | Ongoing, not-yet-released Redux development. Cut from `redux/master`. Never commit directly — changes arrive only via merged feature/bugfix branches. |
| feature/bugfix branches | Every feature or fix starts on its own branch cut off `redux/development`, and is PR'd back into it. The user merges manually. |
| `master` | The **pre-Redux archive** — the old SpaceWarp 1.x codebase (`src/SkipSplashScreen/`, `plugin_template/swinfo.json`, BepInEx plugin, v1.3.0). The branch other Redux projects call `pre-redux`. Reference/history only; don't branch work off it. |
| `dev` (remote only) | Legacy pre-Redux development branch. Dormant. |

`redux/master` was created on 2026-08-06 by renaming the old **`migration/redux`** branch;
`redux/development` was then cut from it, so both start at `c491ae3`. Both were pushed with
upstreams set, and `origin/migration/redux` was deleted — that branch name is fully retired, on
the remote as well as locally. Remote heads are now `master`, `dev`, `redux/master`,
`redux/development`. The GitHub default branch is still `master`; switching it to `redux/master`
is a repo-settings change nobody has made yet.

The `version_check` URL in `swinfo.asset` **and** `swinfo.json` was repointed from
`refs/heads/migration/redux` to **`refs/heads/redux/master`** at the same time — it has to track
the released-version branch, and the old path 404s now that `migration/redux` is gone. That fix
lives on `fix/version-check-url` (commit `c230d4d`), PR **#7** against `redux/development`,
awaiting the user's manual merge. `redux/master` keeps serving the stale URL until a release
merge carries it across.

Fallout that repointing cannot fix: the published **`1.3.1-beta`** pre-release (2025-12-05,
"SkipSplashScreen 1.3.1 for Redux") embeds the old `migration/redux` URL, so its in-game update
check is permanently broken for existing installs. Only a new release ships the corrected URL.
The user accepted this when the remote branch was deleted.

Note that **`CLAUDE.md` and `.claude/` are untracked** — they are local-only working docs, not
committed to the public repo. Don't add them to a commit without asking.

## Porting status (pre-Redux → Redux)

The port lives on the `redux/*` branches described above; `master` is the pre-Redux baseline it
was ported from.

What the migration changed:
- `BaseSpaceWarpPlugin` + `[BepInPlugin]`/`[BepInDependency]` → `KerbalMod`.
- `Config.Bind` (`ConfigEntry<T>`) → `SWConfiguration.Bind` wrapped in `ConfigValue<T>`.
- `Harmony.CreateAndPatchAll(typeof(...))` → `CreateHarmonyAndPatchAll()`.
- `ManualLogSource` → `SWLogger`.
- Splash removal retargeted from `SequentialFlow.AddAction` to
  `PatchManager.Core.Flow.FlowManager.AddActionsToFlow` (veto → post-hoc removal).
- `MyPluginInfo.*` generated constants → hand-written `ModGuid`/`ModName`/`ModVer` consts.
- swinfo `spec` 1.3 → 2.0, new `mod_id`, `main_assembly`, SpaceWarp2 dependency, min KSP2 0.2.3.
- csproj/NuGet build → ThunderKit pipelines + asmdef.

Runtime evidence that the port worked at least once: `redux.log` shows
`'Creating Splash Screens Prefab' successfully removed from FlowActions.` and
`SkipSplashScreen workflow completed.` The auto-load path is not exercised in that log (the
option was off).
