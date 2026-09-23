# Extending the SDK

Start by copying and renaming `Runtime/Sample/PocketCounterGame.cs`. Keep the new game in its own runtime assembly definition, which references `VrcPocketGame.Runtime` and the usual UdonSharp/VRChat assemblies. Add a matching `UdonSharpAssemblyDefinition` asset for that assembly. The sample's `VrcPocketGame.Sample.asmdef` and `.asset` show the required Unity metadata relationship.

Register the new runtime assembly in its own `UdonSharpAssemblyDefinition` asset: set `sourceAssembly` to the new `.asmdef`, not the sample assembly. Create new `.meta` GUIDs when copying assets. The editor assembly must be Editor-only and reference your game assembly, `VrcPocketGame.Runtime`, `VrcPocketGame.Editor`, `UdonSharp.Runtime`, `UdonSharp.Editor`, `VRC.SDK3`, `VRC.SDKBase`, `VRC.Udon`, `Unity.TextMeshPro`, and `UnityEngine.UI`. The sample editor assembly also references `VRC.Udon.Editor` for its validation tooling.

Your game behavior owns game rules, manually synced spectator state, and PlayerData. Give saves a stable game ID prefix in `saveNamespace`, for example `myname.pocket.mygame.v1`. Do not serialize shell claim state into the game, and do not put a terminal-pool index in PlayerData keys.

`PocketGameBehaviour` is the recommended optional base. It provides the public `terminalSession` and `ui` fields, input and claimant helpers, ownership forwarding, and the event hooks below. Derived games keep their own `[UdonBehaviourSyncMode]`. The raw event contract remains supported for games that do not derive from the base.

Override these hooks when deriving from `PocketGameBehaviour` (otherwise implement the raw event names shown in the first column):

| Event | Game responsibility |
| --- | --- |
| `PocketTerminal_OnClaimed` -> `OnTerminalClaimed` | Load local PlayerData and initialize claimant-owned game state. |
| `PocketTerminal_OnReleased` -> `OnTerminalReleased` | Clear or refresh local presentation. |
| `PocketTerminal_OnRecalled` -> `OnTerminalRecalled` | Refresh presentation after the shell moves the terminal. |
| `PocketTerminal_OnUseDown` -> `OnTerminalUseDown` | Perform the pickup-use action; the base applies `CanUseGameInput()`. |
| `PocketTerminal_RequestReturn` -> `OnTerminalReturnRequested` | Save, then return true to synchronously approve stow; return false to refuse. |
| `PocketTerminal_OnAudienceChanged` -> `OnTerminalAudienceChanged` | Optional: refresh owner/spectator presentation. |
| `PocketTerminal_OnReturnStarted` -> `OnTerminalReturnStarted` | Optional: show stowing feedback. |
| `PocketTerminal_OnReturnSucceeded` -> `OnTerminalReturnSucceeded` | Optional: finish or close the game's UI. |
| `PocketTerminal_OnReturnFailed` -> `OnTerminalReturnFailed` | Optional: restore the UI and allow a retry. |
| `PocketTerminal_OnReturnCancelled` -> `OnTerminalReturnCancelled` | Optional: restore the UI. |

Every normal gameplay button and pickup action must use `CanUseGameInput()` (or `session.CanUseGameInput()` without the base). UI buttons that call game methods directly are not gated by the SDK; call `CanUseGameInput()` in each such method. A modal confirmation handler instead checks `IsLocalClaimant()` and its specific modal state, as the sample's `ConfirmReset` does: the ordinary input gate intentionally rejects input while that modal is open. `session.IsLocalClaimant()` already includes `IsCurrentSession()`. The game checks PlayerData and its own schema; the SDK owns claim/session identity, pickup authority, and terminal lifetime. Return approval stays synchronous inside `PocketTerminal_RequestReturn`; the result events arrive afterwards, so a game must not block on them. The scene validator requires the ownership forward to `session.CanPlayerOwnTerminal`; deriving from `PocketGameBehaviour` supplies it.

## Installer outline

Copy the counter sample installer as a reference, then replace the game class and content. In your editor assembly, prepare Udon programs before creating components:

### Kiosk status text

Copy the English defaults and replace any messages you want to customize:

```csharp
// Run from your installer.
var messages = PocketGameTerminalBuilder.DefaultStatusMessages();
messages[PocketGameTerminalPool.StatusIdle] = "Use the kiosk to start or recall your game";
messages[PocketGameTerminalPool.StatusGameReady] = "Your game is ready.";
pool.statusMessages = messages;
```

At runtime, a localizer can assign a translated array after a language change and call `pool.RefreshStatus()` to update the currently displayed message.

```csharp
PocketGameTerminalBuilder.PreparePrograms(typeof(MyGame));
var parts = PocketGameTerminalBuilder.Create(parent, "My terminal", pool, slot, theme,
    new Vector2(800, 520));
var game = parts.GameStateSlot.gameObject.AddUdonSharpComponent<MyGame>();
game.terminalSession = parts.Session;
game.ui = parts.Ui;
game.saveNamespace = "myname.pocket.mygame.v1";
parts.Session.gameEvents = UdonSharpEditorUtility.GetBackingUdonBehaviour(game);
parts.Session.ui = parts.Ui;
PocketGameTerminalBuilder.CopyToUdon(parts.Root);
```

This outline assumes the copied game keeps the sample's field names; import `VrcPocketGame`, `VrcPocketGame.Editor`, `UdonSharpEditor`, and `UnityEngine`. `parent`, `pool`, `slot`, and `theme` come from your installer. `Create` already assigns the session pickup and connects its UI/session references. Add game content to `HeaderSlot`, `ContentSlot`, `ActionSlot`, and `OverlaySlot`. Use `ExternalCanvas` for the external drawer. Assign overlay, external, help, settings, confirmation, and input-modal references on `parts.Ui`, then call `ResetForSession` before the terminal is pooled. Call `CopyToUdon` after all final field assignments, populate the pool's terminal/session arrays as in the sample installer, and validate the completed scene.

Use a profile when the game needs a different physical shell, pickup, or drawer layout. Canvas sizes remain in pixels; offsets, scale, and Rigidbody values describe the physical terminal. Pickup types come from `VRC.SDKBase`:

```csharp
var profile = new PocketGameTerminalProfile
{
    RootScale = .4f,
    ScreenPixelScale = .00066f,
    Mass = .8f,
    Drag = .1f,
    PickupProximity = 1.5f,
    PickupOrientation = VRC_Pickup.PickupOrientation.Grip,
    PickupAutoHold = VRC_Pickup.AutoHoldMode.No,
    ExternalCanvasSize = new Vector2(320, 520)
};
var parts = PocketGameTerminalBuilder.Create(parent, "My terminal", pool, slot, theme, profile);
// Offsets are root-local metres; RootScale scales the whole terminal, drawers included.
var leftDrawer = PocketGameTerminalBuilder.CreateExternalCanvas(parts, "Left drawer",
    new Vector2(320, 520), new Vector3(-.39f, 0, -.025f));
```

When a canvas must change size after creation, use `PocketGameUiBuilder.ResizeCanvas` instead of editing a canvas `RectTransform` directly, so its interactive collider stays in sync. Additional drawers are game-owned for visibility; the SDK toggles only `parts.ExternalCanvas`.

Keep only spectator-facing data in the game's synced fields. Call `RequestSerialization` after claimant-owned changes and refresh presentation in `OnDeserialization`. PlayerData is a separate local persistence channel: load after the SDK accepts the restored claimant, version your schema, and save at meaningful game checkpoints. Return approval confirms that the game allows release; it does not acknowledge a cloud write.
