# Extending the SDK

Start by copying and renaming `Runtime/Sample/PocketCounterGame.cs`. Keep the new game in its own runtime assembly definition, which references `VrcPocketGame.Runtime` and the usual UdonSharp/VRChat assemblies. Add a matching `UdonSharpAssemblyDefinition` asset for that assembly. The sample's `VrcPocketGame.Sample.asmdef` and `.asset` show the required Unity metadata relationship.

Register the new runtime assembly in its own `UdonSharpAssemblyDefinition` asset: set `sourceAssembly` to the new `.asmdef`, not the sample assembly. Create new `.meta` GUIDs when copying assets. The editor assembly must be Editor-only and reference your game assembly, `VrcPocketGame.Runtime`, `VrcPocketGame.Editor`, `UdonSharp.Runtime`, `UdonSharp.Editor`, `VRC.SDK3`, `VRC.SDKBase`, `VRC.Udon`, `Unity.TextMeshPro`, and `UnityEngine.UI`. The sample editor assembly also references `VRC.Udon.Editor` for its validation tooling.

Your game behavior owns game rules, manually synced spectator state, and PlayerData. Give saves a stable game ID prefix in `saveNamespace`, for example `myname.pocket.mygame.v1`. Do not serialize shell claim state into the game, and do not put a terminal-pool index in PlayerData keys.

Implement these methods on the game behavior:

| Event | Game responsibility |
| --- | --- |
| `PocketTerminal_OnClaimed` | Load local PlayerData and initialize claimant-owned game state. |
| `PocketTerminal_OnReleased` | Clear or refresh local presentation. |
| `PocketTerminal_OnRecalled` | Refresh presentation after the shell moves the terminal. |
| `PocketTerminal_OnUseDown` | Perform the pickup-use action, after `session.CanUseGameInput()`. |
| `PocketTerminal_RequestReturn` | Save, then synchronously call `session.PocketTerminal_ApproveReturn()` to allow stow. |
| `PocketTerminal_OnAudienceChanged` | Optional: refresh owner/spectator presentation. |
| `PocketTerminal_OnReturnStarted` | Optional: show stowing feedback. |
| `PocketTerminal_OnReturnSucceeded` | Optional: finish or close the game's UI. |
| `PocketTerminal_OnReturnFailed` | Optional: restore the UI and allow a retry. |
| `PocketTerminal_OnReturnCancelled` | Optional: restore the UI. |

Every normal gameplay button and pickup action must use `session.CanUseGameInput()`. A modal confirmation handler instead checks `session.IsLocalClaimant()` and its specific modal state, as the sample's `ConfirmReset` does: the ordinary input gate intentionally rejects input while that modal is open. The game checks PlayerData and its own schema; the SDK owns claim/session identity, pickup authority, and terminal lifetime. Return approval stays synchronous inside `PocketTerminal_RequestReturn`; the result events arrive afterwards, so a game must not block on them. Forward the game's `OnOwnershipRequest` to `session.CanPlayerOwnTerminal` as in the sample.

## Installer outline

Copy the counter sample installer as a reference, then replace the game class and content. In your editor assembly, prepare Udon programs before creating components:

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

Keep only spectator-facing data in the game's synced fields. Call `RequestSerialization` after claimant-owned changes and refresh presentation in `OnDeserialization`. PlayerData is a separate local persistence channel: load after the SDK accepts the restored claimant, version your schema, and save at meaningful game checkpoints. Return approval confirms that the game allows release; it does not acknowledge a cloud write.
