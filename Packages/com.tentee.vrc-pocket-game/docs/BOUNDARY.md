# Game contract

Required game events are `PocketTerminal_OnClaimed`, `PocketTerminal_OnReleased`, `PocketTerminal_OnRecalled`, `PocketTerminal_OnUseDown`, and `PocketTerminal_RequestReturn`. `PocketTerminal_OnAudienceChanged` and the return-lifecycle events `PocketTerminal_OnReturnStarted`, `PocketTerminal_OnReturnSucceeded`, `PocketTerminal_OnReturnFailed`, and `PocketTerminal_OnReturnCancelled` are optional.

Use `session.CanUseGameInput()` at every game input entry point. It is the public authority gate; do not reconstruct it from ownership checks in game code. A return is approved only when the game calls `session.PocketTerminal_ApproveReturn()` synchronously while handling `PocketTerminal_RequestReturn`.

`PocketGameBehaviour` is the recommended optional base. It forwards the raw event contract to hooks and provides `CanUseGameInput()` and `IsLocalClaimant()`; the raw contract remains supported. Derived games keep their own `[UdonBehaviourSyncMode]`. UI buttons that call game methods directly are not gated by the SDK, so each such method must call `CanUseGameInput()`. `session.IsLocalClaimant()` already includes `IsCurrentSession()`. The scene validator requires a behavior on each game object to forward `OnOwnershipRequest` to `session.CanPlayerOwnTerminal` or derive from `PocketGameBehaviour`.

The game owns PlayerData. Its keys start with a stable game-specific `gameId` namespace and schema version. Pool slots are transport resources, not player identity.
