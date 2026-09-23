# Changelog

## [0.3.2] - 2026-09-23

### Added

- `PocketGameTerminalProfile` and a profile-based `Create` overload, plus `PocketGameTerminalParts.Profile`, `CreateExternalCanvas`, and `ResizeCanvas` for configurable terminals and correctly sized interactive drawers.
- Profile defaults reproduce 0.3.1 terminals; the existing `Create(..., Vector2 screenSize)` overload is unchanged.

## [0.3.1] - 2026-09-23

### Added

- Program-asset, ownership, sync-mode, and modal checks in scene validation, plus a fault driver for aggregated validator failures.

### Changed

- Scene validation is stricter and collects all program-asset problems into one message before running the existing structural checks.

## [0.3.0] - 2026-09-22

### Added

- Optional return-lifecycle game events: `PocketTerminal_OnReturnStarted`, `PocketTerminal_OnReturnSucceeded`, `PocketTerminal_OnReturnFailed`, and `PocketTerminal_OnReturnCancelled`. Started fires when the SDK accepts an approved return; Succeeded fires only after the claim is cleared and the terminal is back in the pool; Failed fires when ownership retries are exhausted; Cancelled fires for a stale or no-longer-valid request.
- `PocketGameTerminalSession` methods `ReportReturnSucceeded()`, `ReportReturnFailed()`, and `ReportReturnCancelled()` for the pool, each delivering at most one result per request. Failed and Cancelled reset the return state so the game can ask again. Games that do not implement the events are unaffected.

### Fixed

- First-run installation no longer fails with "outdated script version": newly generated UdonSharp program assets are stamped with the current script version right after import.
- `CopyToUdon` now reports which behaviour, GameObject, and program asset are not ready, with a distinct message when no program asset is assigned.

## [0.2.0] - 2026-09-21

### Added

- Game-neutral pool/session/input/UI SDK and optional counter sample.
- Separate core/sample assemblies, terminal/UI builders, structural validation, package checker, and ClientSim smoke driver.
- VPM package project layout and GitHub release workflow.

### Breaking changes

- Removed the 0.1.0 slot-specific runtime and installer.
- Replaced its game-coupled surface with `PocketGameTerminalSession` and the five-event contract.
- Generated Udon assets now live in the consuming project's `Assets` folder.

### Validation

- Independent Unity 2022.3.22f1 / Worlds 3.10.5 ClientSim smoke passed claim, action, retry, drawers, modal blocking, scale, stow, restore, reset, and cross-terminal restore.
