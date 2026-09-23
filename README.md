# VRChat Pocket Game SDK

This repository publishes the `com.tentee.vrc-pocket-game` package for VRChat Worlds.

The package is under `Packages/com.tentee.vrc-pocket-game/`. Releases are built by the
included GitHub Actions workflow and are indexed by the
[TenteEEEE VPM Packages](https://tenteeeee.github.io/vpm-repos/) repository.

## Development

1. Open this repository as a Unity 2022.3.22f1 project.
2. Install VRChat Worlds 3.10.5 or later through VCC.
3. Import TextMeshPro Essential Resources.
4. Work from `Packages/com.tentee.vrc-pocket-game/`.
5. Run `python -X utf8 Tools/verify-package.py` before committing.

Package documentation is available in its [README](Packages/com.tentee.vrc-pocket-game/README.md)
and [Japanese README](Packages/com.tentee.vrc-pocket-game/README.ja.md).

## Releasing

1. Bump `package.json` and update `CHANGELOG.md`, then merge the change.
2. Publish a GitHub release with the tag `<version>`; the workflow attaches the package files automatically.
3. Or run **Release VPM package** manually from `main` (or from a version tag to backfill an older release).
4. Set `dry_run` to build and verify the files without publishing.
5. `VPM_REPOS_TOKEN` is optional and lets the workflow trigger the VPM listing rebuild.
6. Without it, run **Build Repo Listing** manually in the `TenteEEEE/vpm-repos` Actions tab.
