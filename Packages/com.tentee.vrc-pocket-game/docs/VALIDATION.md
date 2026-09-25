# Validation record

**Version scope:** The latest recorded Unity/ClientSim run below is for 0.3.2. Version 0.4.0 added `PocketGameBehaviour` and stricter ownership-forward validation; `main` adds more UI APIs and canvas sorting. The table below still summarizes the 0.3.2 run. The 2026-09-25 entry records the ClientSim and MultiSim smoke runs of PR #18 (`3de53aa`) with the multiplayer test changes; the validator-fault driver and rendered-screen review in the table were not repeated then.

On 2026-09-25, the claim-verification backoff fix was checked the same way. With the fix, the Tier 0 smoke passed, including a claim whose terminal ownership was refused for 3 s, with the kiosk showing "Preparing your game…" during the refusal and "Game ready." once accepted (refusal simulated by handing the root back to a remote player from a `Networking._SetOwner` handler registered after ClientSim's). The pool before the fix released that claim about 1.9 s after it was written ("Could not prepare terminal"), so the same smoke failed. With the fix, verification gives up after about 5.2 s. A fresh `InstallAndValidateBatch` and the Tier 1 run script passed.

On 2026-09-25, PR #18 (`3de53aa`, kiosk placement after ownership settles) plus the multiplayer test changes was run in disposable Unity 2022.3.22f1 / Worlds 3.10.5 projects:

| Check | #18 applied | Control: pool before #18 (`f543adb`) |
| --- | --- | --- |
| `InstallAndValidateBatch` from an empty generated folder | Passed, 0 warnings, no `error CS` | — |
| Tier 0 ClientSim smoke | Passed, including simulated remote audience, input guard, ownership-request decisions, departure cleanup, first-claim kiosk placement, stale previous-owner pose and kiosk reuse | Failed at the stale-pose check: the new claim stayed at the previous owner's pose, 5.00 m from the kiosk front. An earlier ordering of the smoke also failed at kiosk reuse (recalled in front of the head) |
| Tier 1 MultiSim v0.2.6 / ParrelSync 1.5.2, host A + late-joining client B | Passed twice: late-join sync, B borrows the slot A stowed 5 m away and it appears and stays at the kiosk front on both editors, kiosk reuse, stow/reclaim, cleanup after B leaves; both processes exited 0 | New-claim placement passed; kiosk reuse failed (3.77 m from the kiosk front) |

The audience-object checks were skipped because the counter sample wires no audience objects. The Tier 1 new-claim placement check passes even without #18 because MultiSim transfers ownership immediately and never calls `OnOwnershipRequest`, so it cannot reproduce the race #18 fixes. Tier 0 simulates that race instead: for 0.4 s after the claim, before the first `VerifyClaim`, a simulated previous owner keeps the root and keeps writing its old pose. Tier 2 was not run.

On 2026-09-23, a documentation audit of `main` after 0.4.0 ran `python -X utf8 Tools/verify-package.py` successfully and checked local Markdown links. This static check does not replace Unity compilation, scene validation, or ClientSim testing.

The scene validator first checks program assets and ownership across the SDK hierarchy. It collects missing, deleted, duplicate, uncompiled, stale, and misplaced program assets, Manual/Continuous sync conflicts on a game event object, ownership target errors, and modal wiring errors into one exception. It logs warnings for graph programs, game programs outside `Assets/VrcPocketGameGenerated`, missing UI session references, unannotated game event sync modes, orphan generated assets, and active saved modal panels. Existing structural and UI layout checks then run per pool.

To exercise the validator's aggregate failures in a disposable Unity project, copy `Tests~/Validation/PocketGameValidatorFaultDriver.cs` into the project's `Assets` folder and run `PocketGameValidatorFaultDriver.RunBatch`. The driver installs the counter sample, checks the clean scene, then checks duplicate assets, a deleted referenced asset, a missing confirmation panel, and a missing `PocketGameBehaviour.terminalSession` reference. It also checks the ownership guard type helper. It exits with code 0 on pass and 1 on failure.

The serialized `SyncMethod` is not compared with the class attribute: UdonSharp rewrites it on scene open, play and build, so a freshly installed scene legitimately differs.

Validated on 2026-09-23 (0.3.2) in a disposable Unity 2022.3.22f1 / Worlds 3.10.5 project: `InstallAndValidateBatch` from an empty generated folder passed with no warnings (the sample's `screenSize` overload now runs through the default profile), `PocketGameValidatorFaultDriver.RunBatch` passed all three fault cases, the ClientSim smoke passed, and a throwaway profile check confirmed that the legacy overload and a null profile reproduce the 0.3.1 dimensions and component defaults, that invalid profiles and `ResizeCanvas` misuse throw, and that a two-terminal scene built with a non-default profile plus a `CreateExternalCanvas` drawer resized by `ResizeCanvas` passed the scene validator with no warnings.

Validated on 2026-09-23 (0.3.1) in a disposable Unity 2022.3.22f1 / Worlds 3.10.5 project: `InstallAndValidateBatch` from an empty generated folder passed with no warnings, `PocketGameValidatorFaultDriver.RunBatch` passed all three fault cases, and a separate game (PocketConveni) installed into an empty scene passed with no warnings.

Validated on 2026-09-21 in a disposable project, separate from the source games.

Recorded environment: Unity 2022.3.22f1, VRChat Worlds/Base 3.10.5, TMP Essential Resources,
ClientSim, Input Handling **Both**, and the usual VCC Worlds scripting defines.

| Check | Result |
| --- | --- |
| `python -X utf8 Tools/verify-package.py` | Passed: metadata, assembly references, package boundary |
| Unity C# and UdonSharp compilation | Passed |
| Sample installation and scene validator | Passed: two terminals, ownership wiring, event targets, UI colliders and button bounds |
| ClientSim batch smoke | Passed: claim, counter input, repeated claim verification, both drawers, modal blocking, scaling, return, saved progress on reborrow, stale return callback, confirmed reset |
| Unity-rendered sample screens | Reviewed: main screen, overlay drawer over the game, separate external drawer |

The smoke runner uses real Udon behaviours and ClientSim PlayerData. It creates a
unique save namespace for each run and allows the pool to choose a different
physical terminal on reborrow.

## Multiplayer test tiers

**Tier 0** is the single-editor ClientSim smoke test. It simulates a remote player
and remote ownership in one scene, so it does not exercise networking. ClientSim's
`SetOwner` never calls `OnOwnershipRequest`, so the smoke invokes the compiled
`_onOwnershipRequest` event directly, using the parameter names from UdonSharp's
event table. That checks the guard's decision logic, not its network behaviour.

**Tier 1** uses VRChatMultiSim with ParrelSync in a validation project only; it
is not a package dependency. Its `com.vrchat.worlds` range is `>=3.10.0 <3.11.0`.
Ownership is broadcast without arbitration, so simultaneous claims and
`OnOwnershipRequest` are not exercised. Tier 1 checks PR #18's kiosk-front placement
for B borrowing A's stowed slot and for kiosk reuse; it does not reproduce the
underlying ownership race. Setup and run order are in
`Tests~/MultiSim/README.md`.

**Tier 2** uses real VRChat clients through SDK Build & Test with multiple
clients. It is the only tier that exercises `OnOwnershipRequest` through real
VRChat networking, along with races and latency.
