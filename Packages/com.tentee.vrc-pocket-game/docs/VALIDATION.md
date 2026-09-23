# Validation record

The scene validator first checks program assets and ownership across the SDK hierarchy. It collects missing, deleted, duplicate, uncompiled, stale, and misplaced program assets, Manual/Continuous sync conflicts on a game event object, ownership target errors, and modal wiring errors into one exception. It logs warnings for graph programs, game programs outside `Assets/VrcPocketGameGenerated`, missing UI session references, unannotated game event sync modes, orphan generated assets, and active saved modal panels. Existing structural and UI layout checks then run per pool.

To exercise the validator's aggregate failures in a disposable Unity project, copy `Tests~/Validation/PocketGameValidatorFaultDriver.cs` into the project's `Assets` folder and run `PocketGameValidatorFaultDriver.RunBatch`. The driver installs the counter sample, checks the clean scene, then checks duplicate assets, a deleted referenced asset, a missing confirmation panel, and a missing `PocketGameBehaviour.terminalSession` reference. It also checks the ownership guard type helper. It exits with code 0 on pass and 1 on failure.

The serialized `SyncMethod` is not compared with the class attribute: UdonSharp rewrites it on scene open, play and build, so a freshly installed scene legitimately differs.

Validated on 2026-09-23 (0.3.2) in a disposable Unity 2022.3.22f1 / Worlds 3.10.5 project: `InstallAndValidateBatch` from an empty generated folder passed with no warnings (the sample's `screenSize` overload now runs through the default profile), `PocketGameValidatorFaultDriver.RunBatch` passed all three fault cases, the ClientSim smoke passed, and a throwaway profile check confirmed that the legacy overload and a null profile reproduce the 0.3.1 dimensions and component defaults, that invalid profiles and `ResizeCanvas` misuse throw, and that a two-terminal scene built with a non-default profile plus a `CreateExternalCanvas` drawer resized by `ResizeCanvas` passed the scene validator with no warnings.

Validated on 2026-09-23 (0.3.1) in a disposable Unity 2022.3.22f1 / Worlds 3.10.5 project: `InstallAndValidateBatch` from an empty generated folder passed with no warnings, `PocketGameValidatorFaultDriver.RunBatch` passed all three fault cases, and a separate game (PocketConveni) installed into an empty scene passed with no warnings.

Validated on 2026-09-21 in a disposable project, separate from the source games.

Environment: Unity 2022.3.22f1, VRChat Worlds/Base 3.10.5, TMP Essential Resources,
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
