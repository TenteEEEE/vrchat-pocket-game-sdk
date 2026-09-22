# Game contract

Required game events are `PocketTerminal_OnClaimed`, `PocketTerminal_OnReleased`, `PocketTerminal_OnRecalled`, `PocketTerminal_OnUseDown`, and `PocketTerminal_RequestReturn`. `PocketTerminal_OnAudienceChanged` and the return-lifecycle events `PocketTerminal_OnReturnStarted`, `PocketTerminal_OnReturnSucceeded`, `PocketTerminal_OnReturnFailed`, and `PocketTerminal_OnReturnCancelled` are optional.

Use `session.CanUseGameInput()` at every game input entry point. It is the public authority gate; do not reconstruct it from ownership checks in game code. A return is approved only when the game calls `session.PocketTerminal_ApproveReturn()` synchronously while handling `PocketTerminal_RequestReturn`.

The game owns PlayerData. Its keys start with a stable game-specific `gameId` namespace and schema version. Pool slots are transport resources, not player identity.
