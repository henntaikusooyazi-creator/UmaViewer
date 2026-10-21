# 原作方式ライブレンダラの進捗

更新日：2026-10-22。通常ライブへ自動採用しない、明示登録の **TestOnly** 開発ブランチです。

## ブランチと本家

- 本家：<https://github.com/katboi01/UmaViewer>
- Fork：<https://github.com/henntaikusooyazi-creator/UmaViewer>
- ブランチ：`feature/original-live-rendering`
- 起点：本家master `07f82e9fa08f23c1cda3a9be3045a09372eae8bb`
- README推奨はUnity 2022.3.62f1、起点のProjectVersionは2022.3.62f3、URPは14.0.12。
- 本家へのPRは未作成。Forkのmasterは同期用とし、この変更は開発ブランチだけに置きます。

## 実装の所在

|機能|Fork上の実装|状態|
|---|---|---|
|右描画→共有入力更新→左描画→下地とのalpha合成|`Assets/Scripts/OriginalRendering/OriginalMultiCameraRenderer.cs`|移植済み|
|原作式によるcoverage生成|`OriginalMaskCoverageGenerator.cs` と専用shader|移植済み|
|選択済み／評価済みcue snapshot|`OriginalCompositeCueAdapter.cs`|移植済み|
|確定しているproducer算術|`OriginalCompositeProducerMath.cs`|移植済み。未解決helperは明示入力|
|実Controlの選択結果通知|`OriginalLiveTimelineBridge.cs`|本家の新しいLiveTimeLine側へ最小差分で接続|
|独立所有cameraによる描画|`OriginalCameraProducer.cs`|移植済み。Built-in／URP14|
|順次描画sessionと入力resolver|`OriginalLiveRenderSession.cs`、`OriginalLiveDrawInputResolver.cs`|移植済み|
|ロード済みtrackの明示登録|`OriginalLiveTimelineRegistration.cs`|移植済み。kとjを独立保持|
|typed groupのComposite選択番号|`LiveTimelineMultiCameraPositionData.MultiCameraNo`|追加済み。旧データでは明示TrackBindingを使用|

新規C# 9ファイル、shader 2ファイルです。既存ソースの変更はControlの通知点とtyped groupの追加fieldのみです。Director、scene、ProjectSettings、Packagesの変更はありません。

研究用Viewerの既存 `CompositeThree` はそのまま保持しています。その実装の変更なしコピーを `Tools/OriginalLiveRendering/Validation/Legacy` に比較テスト用として含めます。既存の通常ライブ出力を置換する機能ではありません。

## 過去の確定成果（今回のFork再検証とは別）

以下は移植前の研究環境・分離Unity等での実証概要です。private入力や採取物は公開しません。

|成果|過去の結果|証拠の適用範囲|
|---|---|---|
|Sequential GPU replay|29チェックPASS|保存中間画像の再生|
|原作mask coverage|34チェックPASS|coverage生成|
|Cue adapter|40チェックPASS|明示／評価済み入力|
|Timeline bridge Built-in／URP|47チェックPASS|typed fixtureと明示camera|
|Real song track registration|21チェックPASS|保存された楽曲のtrack／keyと検証用geometry|
|統合済み分離Unity検証|171/171 PASS|元Viewerの通常ライブ受入ではない|
|原作実FとViewer値|184/184 snapshot一致|観測済みFとjのみ|
|原作8 float値|1,472/1,472 bit一致|選択・補間されたraw値|
|原作実測mask alpha|18,423/18,432完全一致|残9点は最大1/255差|

最後の2項目は原作実測との照合です。人工RGBのGPU fixture検証とは区別します。新しいForkでこれらのprivate入力を再実行したという意味ではありません。

## 今回の検証

|今回実行した検証|結果|限界|
|---|---|---|
|Fork自体のUnity f3 import／コンパイル|未変更SliderControlのCS1010／CS1002で停止|Fork全体の受入は未完了|
|Forkから作成した分離Unity f3・D3D11|35/35チェックPASS|人工入力。通常ライブ・実Controlは対象外|
|engine側C# 5クラスと従来比較用クラス|分離Unityでコンパイル・GPU実行PASS|残るbridge等4クラスのForkでの全結合は未検証|
|新shader 2本と従来比較shader|コンパイル・GPU実行PASS|非公開shader binaryは使用しない|
|coverageの独立式照合|512サンプル、誤差1e-5未満|原作alpha残差9点の再照合ではない|
|順次描画と再利用|右・左・下地alpha保持、次frameの共有RT再利用PASS|人工RGB／alpha|
|Built-in／URP14のcamera描画|両経路PASS|背景色による明示camera入力|
|source camera／RT保護|状態とbuffer内容保持、Built-inのSetTargetBuffers保持PASS|source固有post effect等は対象外|
|従来CompositeThree|変更なしコピーのGPU smoke PASS|研究環境の過去受入全体の再実行ではない|

scene・ProjectSettings・Packagesは変更せず、原作ゲームも起動していません。公開用の人工入力テストは [実行方法](Tools/OriginalLiveRendering/README.md) を参照してください。

## 状態の区別

|段階|状態|
|---|---|
|原作解析でのcue・mask・実draw・合成構造|過去の限定実測で確認済み|
|ローカル研究Viewerの部品実装|実装済み|
|分離UnityでのGPU検証|上記の過去結果を保持|
|Forkへの移植|9クラス・2shader・最小timeline接続を実施|
|ForkのUnityプロジェクト全体|本家由来の構文エラーでコンパイル未完了|
|元Viewer通常ライブへの統合|未完了。自動採用しない|
|全解像度・全frameでの原作一致|未証明|

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

S生成cameraの正式帰属、自然fade・回転helper、有効描画状態の自然供給、全画素対応は未確定です。最新の原作F採取runの停止判定は `PendingManualRecovery` のまま保持します。ゲーム通常終了後の復元と実行中hookの解除証明は別です。このFork作業で原作ゲームや採取環境は操作していません。

残る問題は [KNOWN_ISSUES.md](KNOWN_ISSUES.md)、APIの使用法は [設計文書](Assets/Scripts/OriginalRendering/README.md) を参照してください。
