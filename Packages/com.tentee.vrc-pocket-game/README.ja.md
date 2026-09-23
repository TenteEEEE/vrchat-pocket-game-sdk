# VRChat Pocket Game SDK

VRChat ワールド向けの個人ゲーム端末 SDK です。Core はプール、claim/session、Pickup 入力、共通 UI を担当し、カウンターゲームは別 assembly の任意サンプルです。

## 導入

1. Unity **2022.3.22f1** と VRChat Worlds **3.10.5** を使います。先に **Window > TextMeshPro > Import TMP Essential Resources** を実行します。
2. VCC にカスタム VPM リポジトリ `https://tenteeeee.github.io/vpm-repos/index.json` を追加します。
3. VCC のパッケージ一覧から Worlds プロジェクトへ **VRChat Pocket Game SDK** を追加します。
4. ワールドシーンで **Tools > VRC Pocket Game SDK > Install Counter Sample** を実行します。

メニュー操作は**現在のシーン**にサンプルを追加し、生成 Udon asset を `Assets/VrcPocketGameGenerated` に置きます。シーンは自分で保存してください。`InstallAndValidateBatch` を実行した場合は `Assets/VrcPocketGameGenerated/CounterSample.unity` にサンプルシーンも保存します。いずれも `Packages` は変更しません。

## 新しいゲーム

ゲーム専用 runtime assembly を作り、`PocketGameTerminalBuilder.Create` と `PocketGameUiBuilder` を使います。`PocketGameTerminalProfile` で筐体、画面、Pickup、drawer を設定でき、追加の操作可能な drawer には `CreateExternalCanvas` と `ResizeCanvas` を使います。従来の `Create(..., Vector2 screenSize)` overload も引き続き使えます。ゲームのビヘイビアを `session.gameEvents` に接続します。`session.ui` と `session.pickup` は builder が設定済みです。同期データと PlayerData schema はゲーム側の責務です。`author.game.v1` のような安定した `gameId` namespace を使い、借用した pool slot をキーに含めません。手順全体は [Extending the SDK](docs/EXTENDING.md) を参照してください。すべてのゲーム入力で `session.CanUseGameInput()` を確認します。

`PocketGameBehaviour` は推奨される任意の基底クラスです。`terminalSession` と `ui`、イベントフック、`CanUseGameInput()`、`IsLocalClaimant()` を提供します。基底クラスを使わない場合も、従来のイベント契約を利用できます。派生ゲームは独自の `[UdonBehaviourSyncMode]` を宣言してください。ゲームメソッドを直接呼ぶ UI ボタンは SDK による入力制限を受けないため、各メソッドで `CanUseGameInput()` を呼び出してください。`session.IsLocalClaimant()` には `IsCurrentSession()` の確認も含まれます。シーン検証では、各ゲームオブジェクト上のいずれかのビヘイビアが `OnOwnershipRequest` を `session.CanPlayerOwnTerminal` に転送するか、`PocketGameBehaviour` を継承する必要があります。詳しくは [Extending the SDK](docs/EXTENDING.md) を参照してください。

必須イベントは `PocketTerminal_OnClaimed`、`PocketTerminal_OnReleased`、`PocketTerminal_OnRecalled`、`PocketTerminal_OnUseDown`、`PocketTerminal_RequestReturn` の 5 個です。`PocketTerminal_OnAudienceChanged` は観戦表示用の任意イベントです。返却には 4 個の任意イベントがあります。`PocketTerminal_OnReturnStarted` は SDK が許可済みの返却を受け付けたとき、`PocketTerminal_OnReturnSucceeded` は要求が解決され端末がプールに戻った後だけ、`PocketTerminal_OnReturnFailed` は所有権の再試行が尽きたとき、`PocketTerminal_OnReturnCancelled` は要求が無効になったときに発生します。失敗と取り消しでは返却状態がリセットされ、端末はそのまま使えるため、プレイヤーは再試行できます。返却は同期的で、`PocketTerminal_RequestReturn` 中に保存して `session.PocketTerminal_ApproveReturn()` を呼ぶと許可され、呼ばなければ端末は残ります。

pool だけが `(slot, claimantPlayerId, generation)` を書き込みます。端末 root、SDK session、game behavior は一緒に所有権を移します。overlay drawer は画面上、external drawer は別の world-space panel です。追加 drawer は `extraDrawers` に登録すると UI API から管理でき、modal 表示時に閉じるか選べます。help/settings の個別開閉、複数 confirm、help page 取得、連続倍率の `SetScale(float)` も利用できます。modal/confirm は入力を最優先で遮断し、ゲームを pause しません。Canvas 描画順は固定看板が 0、端末が 10、端末エフェクトが 11 です。独自の半透明 render queue は他の world transparency より後に描かれ、重なりの原因になることがあります。

Scene validator は既存の配線チェックの前に、SDK 階層の Program Asset（欠損・削除済み参照・重複・未コンパイル・`Assets/VrcPocketGameGenerated` 外の配置）、同期モード、所有権対象の分離、`confirmPanel` / `inputModal` の設定を検査し、エラーを 1 つのメッセージにまとめて報告します。検査自体の確認は `Tests~/Validation/PocketGameValidatorFaultDriver.cs` を検証プロジェクトの `Assets` にコピーし、`PocketGameValidatorFaultDriver.RunBatch` を実行します。

`python Tools/verify-package.py` を実行してください。ClientSim は `Tests~/ClientSim/PocketGameSmokeDriver.cs` を検証プロジェクトの `Assets` にコピーし、Input Handling を **Both** にします。通常の VCC Worlds project と `UDON`、`VRC_SDK_VRCSDK3`、`UDONSHARP`、`VRC_ENABLE_PLAYER_PERSISTENCE` を含む VRChat SDK scripting define が必要です。`PocketGameClientSimSmoke.RunBatch` は **`-quit` なし**で実行します。

検証したバージョンと範囲は [検証記録](docs/VALIDATION.md)、リリース履歴は [CHANGELOG](CHANGELOG.md) を参照してください。
