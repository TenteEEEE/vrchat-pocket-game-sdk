# AI coding guide

Source of truth and edit boundary: lifecycle/authority is `Packages/com.tentee.vrc-pocket-game/Runtime/Core`; common visual state is `Packages/com.tentee.vrc-pocket-game/Runtime/UI`; game rules, persistence, and sync payload are game-owned; optional sample code stays in the package's `Runtime/Sample` and `Editor/Sample`; generic construction stays in `Editor/Install`. Do not make Core reference Sample, a game type, a game key, or genre-specific UI. Do not turn builders into a generic framework beyond their current terminal/UI responsibilities.

Preserve these invariants: pool-only claim writes; exact authoritative `(slot, claimant, generation)` identity; terminal root/session/game ownership travel together; game input goes through `session.CanUseGameInput()`; return approval is synchronous inside the request event; PlayerData keys use a stable gameId namespace, never a pool slot. Keep overlay, external drawer, and modal state distinct; modal input never pauses simulation.

Verification mapping: run `python Tools/verify-package.py` for package boundaries; `PrepareProgramAssetsBatch` for generated Udon assets; `ValidateCurrentScene` or `InstallAndValidateBatch` for scene wiring. For ClientSim copy the smoke driver from the package's `Tests~` directory into the validation project's `Assets`, set Input Handling to Both, use a normal VCC Worlds project with `UDON`, `VRC_SDK_VRCSDK3`, `UDONSHARP`, and `VRC_ENABLE_PLAYER_PERSISTENCE`, and run without `-quit`.
For validator fault checks, copy `Tests~/Validation/PocketGameValidatorFaultDriver.cs` into the validation project's `Assets` and run `PocketGameValidatorFaultDriver.RunBatch`.

Do not reintroduce removed slot APIs, edit generated assets under Packages, claim unsupported Udon limitations, or add agents for routine work. Keep changes narrow and readable.
