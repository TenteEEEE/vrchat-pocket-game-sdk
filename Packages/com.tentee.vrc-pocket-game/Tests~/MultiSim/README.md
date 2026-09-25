# Tier 1: VRChat MultiSim two-editor smoke test

This smoke test runs real Udon in two Unity editors. MultiSim connects the original ParrelSync project (host) to one clone (client) over localhost TCP. MultiSim is an optional validation-project dependency only; nothing in `Tests~/MultiSim` is part of the package's compiled assemblies.

## Validation project

Create a normal VCC Worlds project with Unity 2022.3.22f1 and VRChat Worlds 3.10.x. MultiSim v0.2.6 requires `com.vrchat.worlds >=3.10.0 <3.11.0`. Enable `UDON`, `VRC_SDK_VRCSDK3`, `UDONSHARP`, and `VRC_ENABLE_PLAYER_PERSISTENCE` in the project's scripting define symbols. Install/embed `com.veriorpies.parrelsync` and `com.nyakomechan.vrchat-multisim` under `Packages` (MultiSim's ParrelSync dependency is optional at compile time but required for clone role detection).

Copy this driver from the SDK repository to `<project>/Assets/PocketGameMultiSimSmoke.cs`. Ensure the Pocket Game SDK package and its sample/editor scripts are installed in the validation project. Open the original project once and allow assets to import before running the preparation command.

## Create the clone without the GUI

Close both Unity editors before creating the clone. In PowerShell, set `$project` to the full path of the original validation project and create `<project>_clone_0`. The clone uses junctions for shared project content and its own Library:

```powershell
$project = 'C:\VRC\PocketValidation'
$clone = $project + '_clone_0'
New-Item -ItemType Directory -Path $clone
foreach ($name in 'Assets','Packages','ProjectSettings') {
    cmd /c mklink /J "$clone\$name" "$project\$name"
}
Copy-Item -Recurse -LiteralPath "$project\Library" -Destination "$clone\Library"
New-Item -ItemType File -Path "$clone\.clone"
Set-Content -Encoding ascii -NoNewline -LiteralPath "$clone\.parrelsyncarg" -Value 'client'
```

The `Packages` junction means SDK or dependency edits reach the clone immediately, but the clone's Unity editor must reimport those changes before the test. Keep the clone Library separate. The `.clone` marker and `client` argument are read by ParrelSync/MultiSim to identify the client role.

## Run order

Use PowerShell `Start-Process` (not Git Bash) to launch separate Unity processes. Do not add `-quit` to either play run; the driver exits each process after Play Mode finishes. Adjust the Unity executable and project paths as needed.

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe'
$project = 'C:\VRC\PocketValidation'
$clone = $project + '_clone_0'
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath',"$project",'-executeMethod','PocketGameMultiSimSmoke.PrepareSceneBatch','-logFile',"$project\prepare.log") -PassThru
$p.WaitForExit()
```

Preparation installs the sample into a fresh scene and saves `Assets/VrcPocketGameGenerated/CounterSample.unity`. Then run the host and wait for its log marker before starting the client:

```powershell
Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath',"$project",'-executeMethod','PocketGameMultiSimSmoke.RunBatch','-logFile',"$project\host.log")
# Wait until host.log contains: [Pocket MultiSim] HOST READY_FOR_CLIENT
Start-Process -FilePath $unity -ArgumentList @('-batchmode','-projectPath',"$clone",'-executeMethod','PocketGameMultiSimSmoke.RunBatch','-logFile',"$clone\client.log")
```

The run opens the saved scene and never saves it or regenerates Udon assets. `MultiSim.Enabled` and the ClientSim settings (`com.vrchat.clientsim.settings`) are machine-wide EditorPrefs, so do not open other ClientSim or MultiSim projects during this run. Each process restores both when its run ends; a killed run leaves `MultiSim.Enabled` on and ClientSim forced to `enableClientSim`, `spawnPlayer`, `localPlayerIsMaster` and `initializationDelay = 0`, so reset them afterwards if needed. Check both process exit codes and the `[Pocket MultiSim] PASS` / `FAIL` / `SKIP` log lines. Run `PrepareSceneBatch` again before each pair of runs so every pass gets a fresh save namespace.

Wait on the returned process (`$p = Start-Process ... -PassThru; $p.WaitForExit()`) rather than `Start-Process -Wait`, which also waits for Unity's helper processes. A batch editor can hang during shutdown after it has logged `run complete with exit code N` (seen once in the clone, inside the VCC resolver while mono was cleaning up). The logged code is the result; stop the process if it lingers.

## Scope and limits

Tier 1 checks a late join, replicated claim table and active object pool state, spectator presentation (the pickup is disabled; the counter sample wires no audience objects, so that check reports `SKIP`), synced counter value, and cleanup after the client leaves. It also checks PR #18's placement outcomes: B borrows the slot A stowed far away and sees it at the kiosk front, then reuses the kiosk and sees the terminal return to the kiosk front instead of the head-relative recall position. The client waits for the host to reclaim before continuing, avoiding simultaneous pool ownership claims.

Tier 1 cannot reproduce the underlying PR #18 ownership race: MultiSim never calls `OnOwnershipRequest` and transfers ownership immediately. A pass checks the placement outcomes only and does not prove the race fix. Tier 2 real-client runs are needed for that. MultiSim also does not test realistic latency, bandwidth, or real VRChat networking.
