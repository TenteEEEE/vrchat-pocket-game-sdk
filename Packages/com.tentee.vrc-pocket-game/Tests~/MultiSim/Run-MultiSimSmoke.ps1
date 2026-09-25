# Runs the Tier 1 MultiSim smoke end to end: prepare the scene, start the host,
# wait for READY_FOR_CLIENT, start the clone as a late joiner, then print both results.
# Usage: .\Run-MultiSimSmoke.ps1 -Project C:\VRC\PocketValidation [-Clone ...] [-Unity ...] [-Tag run1]
param(
    [Parameter(Mandatory = $true)][string]$Project,
    [string]$Clone = "$Project`_clone_0",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\2022.3.22f1\Editor\Unity.exe",
    [string]$Tag = "run"
)
$ErrorActionPreference = "Stop"

function Wait-Log($path, $pattern, $seconds) {
    $end = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $end) {
        if ((Test-Path $path) -and (Select-String -Path $path -Pattern $pattern -Quiet)) { return $true }
        Start-Sleep -Seconds 1
    }
    return $false
}

# Wait on the process itself; Start-Process -Wait also waits for Unity's helper processes.
$prepare = Start-Process $Unity -ArgumentList @("-batchmode", "-projectPath", $Project, "-executeMethod", "PocketGameMultiSimSmoke.PrepareSceneBatch", "-logFile", "$Project\prepare-$Tag.log") -PassThru
$prepare.WaitForExit()
"prepare exit=$($prepare.ExitCode)"
if ($prepare.ExitCode -ne 0) { exit 1 }

$hostLog = "$Project\host-$Tag.log"
$clientLog = "$Clone\client-$Tag.log"
foreach ($log in @($hostLog, $clientLog)) { if (Test-Path $log) { Remove-Item $log } }
$hostProcess = Start-Process $Unity -ArgumentList @("-batchmode", "-projectPath", $Project, "-executeMethod", "PocketGameMultiSimSmoke.RunBatch", "-logFile", $hostLog) -PassThru
if (-not (Wait-Log $hostLog "READY_FOR_CLIENT|\[Pocket MultiSim\] FAIL|error CS" 600)) { "host did not become ready" }
$clientProcess = Start-Process $Unity -ArgumentList @("-batchmode", "-projectPath", $Clone, "-executeMethod", "PocketGameMultiSimSmoke.RunBatch", "-logFile", $clientLog) -PassThru
$null = Wait-Log $hostLog "run complete|\[Pocket MultiSim\] FAIL" 900
$null = Wait-Log $clientLog "run complete|\[Pocket MultiSim\] FAIL" 120

# An editor can hang in shutdown after logging its result; the logged exit code is authoritative.
foreach ($process in @($hostProcess, $clientProcess)) {
    if (-not $process.WaitForExit(60000)) { "stopping lingering Unity pid $($process.Id)"; Stop-Process -Id $process.Id -Force }
}
"host exit=$($hostProcess.ExitCode) client exit=$($clientProcess.ExitCode)"
"== HOST"; Select-String -Path $hostLog -Pattern "\[Pocket MultiSim\] (PASS|FAIL|SKIP|HOST|host saw|pool owner|run complete)" | ForEach-Object { $_.Line }
"== CLIENT"; Select-String -Path $clientLog -Pattern "\[Pocket MultiSim\] (PASS|FAIL|SKIP|CLIENT|run complete)" | ForEach-Object { $_.Line }
# The logged result is authoritative even when a lingering editor had to be stopped.
$passed = (Select-String -Path $hostLog -Pattern "run complete with exit code 0" -Quiet) -and (Select-String -Path $clientLog -Pattern "run complete with exit code 0" -Quiet)
if ($passed) { "RESULT: PASS"; exit 0 } else { "RESULT: FAIL"; exit 1 }
