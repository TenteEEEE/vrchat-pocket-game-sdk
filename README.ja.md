# VRChat Pocket Game SDK

**VRChat のワールドに、手に持って遊べる個人用ゲームを。** プレイヤーはキオスクから端末を呼び、手に取って遊び、しまってからまた呼び戻せます。端末の貸し出しや所有権は SDK が担当し、画面の中で遊ぶゲームは制作者が作ります。

[English](README.md) · [VPM リポジトリ](https://tenteeeee.github.io/vpm-repos/) · [パッケージの詳細](Packages/com.tentee.vrc-pocket-game/README.ja.md)

![キオスクから共用の端末を呼び、手に持って遊び、返却や再呼び出しをする流れ](docs/images/pocket-game-flow.svg)

同梱の**カウンターゲームは動作例**です。SDK は手持ち端末の筐体、Pickup 入力、セッションと所有権の確認、共通 UI を提供します。ゲームのルールや演出、観戦者向けの同期データ、PlayerData の保存形式は各ゲームが決めます。

## カウンターのサンプルを試す

1. Unity **2022.3.22f1**、VRChat Worlds **3.10.5 以降**の Worlds プロジェクトを用意します。**Window > TextMeshPro > Import TMP Essential Resources** を先に実行します。
2. VRChat Creator Companion に `https://tenteeeee.github.io/vpm-repos/index.json` をカスタム VPM リポジトリとして登録し、**VRChat Pocket Game SDK** を追加します。
3. ワールドシーンを開き、**Tools > VRC Pocket Game SDK > Install Counter Sample** を実行します。現在のシーンにキオスクと、プールされた端末 2 台が追加されます。
4. ローカル検証用に ClientSim を導入して Play モードに入ります。キオスクから端末を呼んで手に持ち、**+1** または Pickup Use で操作します。**Settings > Save & stow** で返却し、再度呼ぶと保存されたカウントを確認できます。

インストーラーは生成 Udon アセットを `Assets/VrcPocketGameGenerated` に配置し、`Packages` 以下は変更しません。サンプルでは観戦表示、画面上のドロワー、独立したワールド空間のドロワー、モーダル表示中の入力制限も確認できます。詳細は[パッケージの README](Packages/com.tentee.vrc-pocket-game/README.ja.md)と[検証記録](Packages/com.tentee.vrc-pocket-game/docs/VALIDATION.md)を参照してください。

## 自分のゲームを作る

![SDK は端末のプール、セッション、Pickup 入力、共通 UI を担当し、ゲームはルール、同期データ、保存形式を担当する](docs/images/pocket-game-boundary.svg)

[カウンターゲームとインストーラー](Packages/com.tentee.vrc-pocket-game/docs/EXTENDING.md)を参考に、ゲーム専用の runtime assembly を作ります。`PocketGameTerminalBuilder` で端末を、`PocketGameUiBuilder` で UI を作り、ゲームのビヘイビアをセッションに接続します。`PocketGameTerminalProfile` で筐体、画面、Pickup、ドロワーの配置も調整できます。

端末イベントには `PocketGameBehaviour` のフックを使うか、イベント契約を直接実装します。UI ボタンから直接呼ぶメソッドも含め、すべてのゲーム入力で `CanUseGameInput()` を確認してください。保存先には `author.game.v1` のような安定したゲーム ID を使います。プールのスロット番号は端末の識別子であり、セーブの識別子ではありません。assembly の設定からイベント、同期、検証までは[拡張ガイド](Packages/com.tentee.vrc-pocket-game/docs/EXTENDING.md)を参照してください。

## 開発者向け

パッケージ本体は [`Packages/com.tentee.vrc-pocket-game/`](Packages/com.tentee.vrc-pocket-game/) にあります。このリポジトリを Unity 2022.3.22f1 で開き、VCC から VRChat Worlds 3.10.5 以降を導入し、TMP Essential Resources をインポートしてください。コミット前に `python -X utf8 Tools/verify-package.py` を実行します。[設計](Packages/com.tentee.vrc-pocket-game/docs/ARCHITECTURE.md)と[パッケージ境界](Packages/com.tentee.vrc-pocket-game/docs/BOUNDARY.md)も参照できます。

### リリース

1. `CHANGELOG.md` を更新してマージします。リリース版のバージョンはタグから決まるため、リリースのために `package.json` を編集しません。
2. `x.x.x` または `vx.x.x` 形式のタグを push します。ワークフローが VPM メタデータにバージョンを書き込み、検証後に GitHub Release とパッケージを公開します。
3. 過去のバージョンを補完する場合は、バージョンタグから **Release VPM package** を手動実行できます。ブランチからの実行は dry run のみです。
4. `dry_run` を指定すると、公開せずにビルドと検証を実行します。
5. `VPM_REPOS_TOKEN` があれば VPM 一覧の更新を起動できます。ない場合は `TenteEEEE/vpm-repos` の Actions で **Build Repo Listing** を実行します。
