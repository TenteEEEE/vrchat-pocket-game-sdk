# Verification and migration

Run `python Tools/verify-package.py` after package metadata, assembly, or boundary changes. For Unity scene work run `PrepareProgramAssetsBatch`, then `ValidateCurrentScene` or `InstallAndValidateBatch`.

For a disposable ClientSim project, copy `Tests~/ClientSim/PocketGameSmokeDriver.cs` to `Assets`, select **Both** for Input Handling, and use a normal VCC Worlds project with `UDON`, `VRC_SDK_VRCSDK3`, `UDONSHARP`, and `VRC_ENABLE_PLAYER_PERSISTENCE` SDK scripting defines. Invoke `PocketGameClientSimSmoke.RunBatch` without `-quit`. The smoke test passed in an independent Unity 2022.3.22f1 / Worlds 3.10.5 project, exercising real Udon claim, action, retry, drawers, modal blocking, scale, stow, restore, reset, and restore on a different borrowed pool terminal. Unity render review also passed for normal, overlay, and external-drawer screens. See [validation results](VALIDATION.md).

Version 0.2.0 removes the 0.1.0 slot-specific runtime and installer. Migrate games to an independent game behavior wired through `PocketGameTerminalSession`, the five required events, and a game-owned save namespace.
