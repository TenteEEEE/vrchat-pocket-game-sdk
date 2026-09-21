# VRChat Pocket Game SDK

VRChat ワールド向けの個人ゲーム端末 SDK です。Core はプール、claim/session、Pickup 入力、共通 UI を担当し、カウンターゲームは別 assembly の任意サンプルです。

## 導入

1. Unity **2022.3.22f1** と VRChat Worlds **3.10.5** を使います。先に **Window > TextMeshPro > Import TMP Essential Resources** を実行します。
2. VCC にカスタム VPM リポジトリ `https://tenteeeee.github.io/vpm-repos/index.json` を追加します。
3. VCC のパッケージ一覧から Worlds プロジェクトへ **VRChat Pocket Game SDK** を追加します。
4. ワールドシーンで **Tools > VRC Pocket Game SDK > Install Counter Sample** を実行します。

生成 Udon asset と sample scene は `Assets/VrcPocketGameGenerated` に置かれ、`Packages` は変更しません。

## 新しいゲーム

ゲーム専用 runtime assembly を作り、`PocketGameTerminalBuilder.Create` と `PocketGameUiBuilder` を使います。session の `gameEvents`、`ui`、`pickup` を設定します。同期データと PlayerData schema はゲーム側の責務です。`author.game.v1` のような安定した `gameId` namespace を使い、借用した pool slot をキーに含めません。手順全体は [Extending the SDK](docs/EXTENDING.md) を参照してください。すべてのゲーム入力で `session.CanUseGameInput()` を確認します。

必須イベントは `PocketTerminal_OnClaimed`、`PocketTerminal_OnReleased`、`PocketTerminal_OnRecalled`、`PocketTerminal_OnUseDown`、`PocketTerminal_RequestReturn` の 5 個です。`PocketTerminal_OnAudienceChanged` は観戦表示用の任意イベントです。返却は同期的で、`PocketTerminal_RequestReturn` 中に保存して `session.PocketTerminal_ApproveReturn()` を呼ぶと許可され、呼ばなければ端末は残ります。

pool だけが `(slot, claimantPlayerId, generation)` を書き込みます。端末 root、SDK session、game behavior は一緒に所有権を移します。overlay drawer は画面上、external drawer は別の world-space panel です。modal/confirm は入力を最優先で遮断し、ゲームを pause しません。

`python Tools/verify-package.py` を実行してください。ClientSim は `Tests~/ClientSim/PocketGameSmokeDriver.cs` を検証プロジェクトの `Assets` にコピーし、Input Handling を **Both** にします。通常の VCC Worlds project と `UDON`、`VRC_SDK_VRCSDK3`、`UDONSHARP`、`VRC_ENABLE_PLAYER_PERSISTENCE` を含む VRChat SDK scripting define が必要です。`PocketGameClientSimSmoke.RunBatch` は **`-quit` なし**で実行します。

独立した Unity 2022.3.22f1 / Worlds 3.10.5 project で ClientSim smoke test は通過しました。実 Udon の claim、action、retry、drawers、modal、scale、stow、restore、reset と、別 pool terminal を借りた後の保存値復元を確認しています。通常画面、overlay、external drawer の Unity render 目視確認も通過しました。詳細は [検証結果](docs/VALIDATION.md) を参照してください。

## 0.2.0 の破壊的変更

0.1.0 の slot 専用 runtime/installer は削除されました。`PocketGameTerminalSession` と 5-event contract がゲーム結合 surface を置き換え、生成 asset は consuming project の `Assets` に置かれます。
