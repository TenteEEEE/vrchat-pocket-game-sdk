# Architecture

`Runtime/Core` and `Runtime/UI` are cooperating parts of the same SDK runtime assembly: the session holds an optional UI reference and the UI holds a session reference for input gates. Neither may reference `Runtime/Sample`. The optional sample runtime assembly depends on the SDK runtime assembly. `Editor/Install` is a game-neutral editor assembly; the separate sample editor assembly depends on it and on the sample runtime.

`PocketGameTerminalPool` owns allocation, the small synced claim table, retry, and cleanup. `PocketGameTerminalSession` mirrors an assigned claimant and generation, gates ownership/input, and forwards the fixed event vocabulary to the game. `PocketGameInputRelay` forwards pickup use and rejects ownership requests that do not match the authoritative claimant. `PocketGameUi` owns overlay/external drawer state, tabs, scale, help/settings, confirmation, and modal input blocking. It never pauses simulation.

The authority boundary is deliberate: only the pool writes claims; root, session, and game behavior ownership move as a group; a game is free to define its own schema and synced payload. The sample counter only demonstrates this boundary and is not required by the core SDK.

## Extraction from the two games

PocketOverdrive and PocketConveni share the personal terminal lifecycle: borrow from a kiosk, hold or recall the terminal, route claimant input, show spectator state, and return it. They also share a main screen with an overlay drawer, an external drawer, help/settings, and confirmation UI. These are the reusable SDK responsibilities.

Their game rules are deliberately excluded: reels, scoring, rewards, shop/inventory progression, game-specific animations, artwork, audio, and save schemas. Persistence readiness belongs to the session lifecycle; persistence keys, migrations, and saved values belong to each game. The counter sample demonstrates that separation without pulling either original game's rules into the SDK.

The repository is `vrchat-pocket-game-sdk`; the VPM package ID is `com.tentee.vrc-pocket-game`, and the display name is **VRChat Pocket Game SDK**.
