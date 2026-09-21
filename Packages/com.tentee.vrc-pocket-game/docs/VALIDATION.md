# Validation record

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
