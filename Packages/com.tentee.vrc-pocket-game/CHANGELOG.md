# Changelog

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
