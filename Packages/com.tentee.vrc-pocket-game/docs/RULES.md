# Verification

Run `python Tools/verify-package.py` after package metadata, assembly, or boundary changes. For Unity scene work run `PrepareProgramAssetsBatch`, then `ValidateCurrentScene` or `InstallAndValidateBatch`.

For a disposable ClientSim project, copy `Tests~/ClientSim/PocketGameSmokeDriver.cs` to `Assets`, select **Both** for Input Handling, and use a normal VCC Worlds project with `UDON`, `VRC_SDK_VRCSDK3`, `UDONSHARP`, and `VRC_ENABLE_PLAYER_PERSISTENCE` SDK scripting defines. Invoke `PocketGameClientSimSmoke.RunBatch` without `-quit`. See the [validation record](VALIDATION.md) for tested versions and results.
