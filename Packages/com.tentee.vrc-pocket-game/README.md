# VRChat Pocket Game SDK

A game-neutral SDK for pooled personal terminals in VRChat worlds. Core supplies claim/session authority, pickup input, and common UI; the counter game is an optional sample in separate runtime and editor assemblies.

## Install and run the sample

1. Use Unity **2022.3.22f1** and VRChat Worlds **3.10.5**. Import TMP Essential Resources first: **Window > TextMeshPro > Import TMP Essential Resources**.
2. Add `https://tenteeeee.github.io/vpm-repos/index.json` as a custom VPM repository in VCC.
3. Add **VRChat Pocket Game SDK** to a Worlds project from VCC's package list.
4. Open a world scene, then run **Tools > VRC Pocket Game SDK > Install Counter Sample**.

The installer writes generated Udon assets and the sample scene under `Assets/VrcPocketGameGenerated`; it does not write into `Packages`. It creates a kiosk, pickup/stow/recall terminals, owner-only counter input, spectator sync, PlayerData persistence, an overlay drawer, and a separate external world-space drawer.

## Create a new game

Create a game-specific runtime assembly. Build terminals through `PocketGameTerminalBuilder.Create` and UI through `PocketGameUiBuilder`; use `PocketGameTerminalProfile` for shell, screen, pickup, and drawer settings, and `CreateExternalCanvas`/`ResizeCanvas` for additional interactive drawers. The old `Create(..., Vector2 screenSize)` overload remains available. Do not copy the sample installer as a framework. Assign the session's `gameEvents`, `ui`, and `pickup` fields. The game owns its synced data and save schema. Use a durable `gameId` namespace such as `author.game.v1`; never use a borrowed pool slot in PlayerData keys. See [Extending the SDK](docs/EXTENDING.md) for a complete installer sequence.

Every game input entry point must call `session.CanUseGameInput()`. It combines local claimant authority, accepted session identity, return state, and modal state.

`PocketGameBehaviour` is the recommended optional base: it exposes `terminalSession` and `ui`, forwards the terminal events to hooks, and supplies `CanUseGameInput()` and `IsLocalClaimant()`. The raw event contract above remains supported. Derived games keep their own `[UdonBehaviourSyncMode]`. UI buttons that call game methods directly bypass the SDK input gate, so each such method must call `CanUseGameInput()`. `session.IsLocalClaimant()` already includes `IsCurrentSession()`. The scene validator now requires at least one behavior on each game object to forward `OnOwnershipRequest` to `session.CanPlayerOwnTerminal` or derive from `PocketGameBehaviour`.

## Contract and authority

Games implement five required events: `PocketTerminal_OnClaimed`, `PocketTerminal_OnReleased`, `PocketTerminal_OnRecalled`, `PocketTerminal_OnUseDown`, and `PocketTerminal_RequestReturn`. `PocketTerminal_OnAudienceChanged` is optional and is useful for spectator-only presentation.

Four optional return-lifecycle events are also available: `PocketTerminal_OnReturnStarted` when the SDK accepts an approved return, `PocketTerminal_OnReturnSucceeded` only after the claim is cleared and the terminal is back in the pool, `PocketTerminal_OnReturnFailed` when ownership retries are exhausted, and `PocketTerminal_OnReturnCancelled` for a stale or no-longer-valid request. A failed or cancelled return resets the return state, so the terminal stays usable and the player can try again.

Return approval is synchronous: during `PocketTerminal_RequestReturn`, save game-owned state and call `session.PocketTerminal_ApproveReturn()`. If it is not called in that event, the terminal remains active. The pool revalidates the claimant and generation before it releases the slot.

The pool alone writes `(slot, claimantPlayerId, generation)`. A terminal accepts a callback only for that exact authoritative tuple after terminal root, SDK session, and game behavior ownership have reached the claimant. Those three objects transfer ownership together.

## UI and validation

An overlay drawer lives above the main game screen. An external drawer is a separate world-space panel. Register additional game-owned drawers in `extraDrawers` to manage them through the UI API and optionally close them with modals. Help/settings panels, indexed confirmations, help-page queries, and continuous `SetScale(float)` are available alongside the legacy methods. Modal and confirmation UI take priority and block game input; UI does not pause game simulation. World-space canvas sorting uses order 0 for fixed signage, 10 for terminal canvases, and 11 for terminal effects; custom transparent render queues can overlap unrelated world transparency.

The scene validator checks program assets, ownership, sync mode, modal wiring, and the existing structural/UI layout rules. It collects program-asset and ownership problems in one message. The repository runs `python -X utf8 Tools/verify-package.py` before publishing. Unity batch entry points are `VrcPocketGame.Editor.PocketGameInstaller.PrepareProgramAssetsBatch`, `ValidateCurrentScene`, and `InstallAndValidateBatch`. Copy `Tests~/Validation/PocketGameValidatorFaultDriver.cs` into a validation project's `Assets` and run `PocketGameValidatorFaultDriver.RunBatch` to exercise validator failures.

For ClientSim, copy `Tests~/ClientSim/PocketGameSmokeDriver.cs` into the validation project's `Assets` folder, set Player **Input Handling** to **Both**, and use a normal VCC Worlds project with its VRChat SDK scripting defines (including `UDON`, `VRC_SDK_VRCSDK3`, `UDONSHARP`, and `VRC_ENABLE_PLAYER_PERSISTENCE`). Run `VrcPocketGame.Editor.PocketGameClientSimSmoke.RunBatch` without `-quit`.

The ClientSim smoke test passed in an independent Unity 2022.3.22f1 / Worlds 3.10.5 project. It exercised real Udon claim, action, retry, drawers, modal blocking, scale, stow, restore, reset, and restore after borrowing a different pool terminal. Unity renders of the normal, overlay, and external-drawer screens also passed visual review. See [validation results](docs/VALIDATION.md).

## Breaking changes in 0.2.0

The former 0.1.0 slot-oriented runtime and installer are removed. `PocketGameTerminalSession` and the five-event contract replace the old game-coupled surface. Generated assets now belong under the consuming project's `Assets` folder.
