# 原作方式ライブレンダラの進捗

更新日：2026-10-23。通常ライブへ自動採用しない、明示登録の **TestOnly** 開発ブランチです。

> この文書は独立検証ブランチ `codex/validate-original-live-integration` の状態です。公開済み `feature/original-live-rendering` は `781ef89`（7コミット）のまま保持しています。

## 二channelの自然供給と3分割preview

既存の連続providerへ、明示順序を持つ実楽曲の二channelを接続しました。有効keyの同時選択、同じowner時刻、異なるViewer camera参照を保持し、左右coverage→下地→最終合成を同一更新で観測します。公開人工入力は **338/338 PASS**、保存実測を含む結合検証は **1820/1820 PASS**（従来から追加26件）です。

ローカル実楽曲を通常の終端・Viewer復帰まで自然再生し、二channelが同時有効な各更新を描画しました。同じ更新で保存した左右coverageと下地からの再合成は最終出力と全RGBA一致し、左右・下地それぞれのRGB寄与と三領域も確認しました。修正後の同じsourceで、従来の単一channel・正duration fadeを含む自然再生も終端・復帰まで再確認しました。原作の追加起動・ARM・採取は不要でした。

追加のViewer preview policyは「無効key・解決済みfade 0・pass 0」のchannelを透明submissionにできます。kind 0の未知の厚さはsnapshotとLastUnresolvedに残し、有効key、fade未取得、別passには適用しません。既存constructorは従来の保留を維持します。[自然再生と同一更新の証拠](Tools/OriginalLiveRendering/INTEGRATION.md#二channelの自然再生と同一更新の証拠) を参照してください。

完全replayは全RGBAで比較し、各cameraの可視寄与はRGBだけで判定します。destination alphaの差を可視寄与に含めません。原作TextureMapping・S生成camera帰属・時刻原点は引き続き未確定です。

## 自然なkey・owner時刻の連続供給（前回）

`OriginalLiveContinuousProvider` がロード済みControlの各更新を受け取り、選択済みkeyと同じowner時刻の組から既存resolverを呼びます。無効keyも通知し、owner時刻を1更新で使い切ることで、初回・late updateだけの再実行ではUnknownを保ちます。停止・seek、登録所有権、未取得時の画素保持、単一／二channelを含む追加64件により、公開人工入力は **312/312 PASS**、保存実測を含む結合試験は **1794/1794 PASS** です。

明示callback付きの既存楽曲loaderから、単一trackのローカル実楽曲をモデル・舞台・音声とともにロードし、通常Director更新で生成するViewer previewへ接続しました。**通常の楽曲終端・scene復帰まで自然再生PASS**。native物理は初期化前の明示opt-outを使い、既存UIの既定動作は保持します。[接続・再生受入](Tools/OriginalLiveRendering/INTEGRATION.md#自然な連続供給とviewer再生) を参照してください。

このpreviewのTextureMappingは、Viewerが生成するfull-viewport・mipなしRTの明示設定です。原作の自然TextureMappingやcamera正式帰属へ昇格しません。原作からの時刻stream、原作とViewerの時刻原点・音声epoch、原作最終画素の全面一致は未成立です。各false判定と `Phase257cause=Unknown` を維持します。

## 自然な正duration keyのfade接続（前回）

別楽曲の有効keyから期間内15点・終点1点を自然採取し、実owner時刻・比較整数・raw durationを使う生成fadeが原作setter入力とbit一致しました。同じ実keyを選択する実Control → resolver → GPUもBuilt-in／URP14の全RGBAで一致し、従来1524＋追加206の **1730/1730 PASS**。公開用の人工入力は **248/248 PASS** です。[検証範囲](Tools/OriginalLiveRendering/INTEGRATION.md#正のdurationを持つ自然keyのfade) を参照してください。

今回の原作runは独立して正常停止・通常終了・所有接続復元を確認しました。過去のPending記録は変更しません。自然fadeの実測は1keyの限定区間であり、原作とViewerの時刻原点、自然TextureMapping・camera、楽曲通しの受入は別の未確定点です。各false判定と `Phase257cause=Unknown` を維持します。

## 実楽曲cueの角度生成と自然fade条件

保存した実楽曲のtyped cueとauthored curveを実Controlへ渡し、確認済みprofileから生成したshader角度を独立したreceiverの保存値と照合しました。184観測の角度2成分がすべてbit一致し、Built-in／URP14の人工GPU画像も全RGBAで一致しました。従来228件を含む **1524/1524 PASS** です。[入力・検証・制約](Tools/OriginalLiveRendering/INTEGRATION.md#保存した実楽曲cueからの角度生成) を参照してください。

対象の保存keyには正のdurationCountを持つ有効keyがなく、自然fade遷移は未証明です。今回の追加原作runはありません。観測元の元Pending判定を維持し、別runのowner時刻と結合していません。camera geometry・描画色・TextureMappingは人工入力で、通常ライブの通し再生を示すものではありません。

## owner比較入力の接続

ownerの現在時刻と比較時刻に対する保存済みproducer演算を確認し、明示opt-in時の実 `AlterUpdate` から既存resolverへ接続しました。実採取したowner入力を外部から与える検証は **228/228 PASS**。従来175件と追加53件を含み、Built-in／URP14で人工geometry・typed keyからの全RGBA一致を確認しています。[接続と受入範囲](Tools/OriginalLiveRendering/INTEGRATION.md#owner比較値の接続受入) を参照してください。

通常ライブの自動採用、原作とViewerの時刻原点・音声epoch・最終画面frameの結合は未確定です。採取・検証器の失敗は保存証拠の再照合と区別し、元のPending判定を書き換えていません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false` を維持します。

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
|確定しているproducer算術|`OriginalCompositeProducerMath.cs`|移植済み。有効thicknessの明示依存による分岐を追加。未解決helperは明示入力|
|実Controlの選択結果通知|`OriginalLiveTimelineBridge.cs`|本家の新しいLiveTimeLine側へ最小差分で接続|
|独立所有cameraによる描画|`OriginalCameraProducer.cs`|移植済み。Built-in／URP14|
|順次描画sessionと入力resolver|`OriginalLiveRenderSession.cs`、`OriginalLiveDrawInputResolver.cs`|移植済み|
|ロード済みtrackの明示登録|`OriginalLiveTimelineRegistration.cs`|移植済み。kとjを独立保持|
|typed groupのComposite選択番号|`LiveTimelineMultiCameraPositionData.MultiCameraNo`|追加済み。旧データでは明示TrackBindingを使用|

初回移植はC# 9ファイル、shader 2ファイルでした。後続の明示provider・算術・連続previewは各節に記録しています。今回の既存ソース変更はbridgeの時刻寿命、Controlの無効key通知、loaderの明示callbackと物理opt-out、camera不在時の視線追従guardです。Director、scene、ProjectSettings、Packagesの保存内容は保持します。

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
|公開済み描画ブランチのUnityプロジェクト全体|本家由来の構文エラーでコンパイル未完了。7コミットの状態を保持|
|独立検証ブランチのmanaged／Editorコンパイル|Unity f3のbatch import終了コード0|
|独立検証ブランチの9クラス結合|実Control＋人工typed key／geometryで152/152 PASS。従来94＋厚さ算術12＋provider結合46|
|元Viewer通常ライブへの統合|未完了。自動採用しない|
|全解像度・全frameでの原作一致|未証明|

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

S生成cameraの正式帰属、自然fade・回転helper、有効描画状態の自然供給、全画素対応は未確定です。最新の原作F採取runの停止判定は `PendingManualRecovery` のまま保持します。ゲーム通常終了後の復元と実行中hookの解除証明は別です。このFork作業で原作ゲームや採取環境は操作していません。

残る問題は [KNOWN_ISSUES.md](KNOWN_ISSUES.md)、APIの使用法は [設計文書](Assets/Scripts/OriginalRendering/README.md) を参照してください。

## 独立検証ブランチでの追加結果

この節は上記35チェックとは別の検証です。描画9クラスと2shaderの実装は `781ef89` から変更していません。

- PR #234全体ではなく、その最初のコンパイル修正 `fbaaa925ab54c9f1f472a8ee7428b7e6ebc7fbff` を出典付きで適用しました。
- 本家ソースの不正なUTF-8文字列を修正し、既存GBKソース8ファイルを可逆にUTF-8へ変換しました。数式や制御処理の変更ではありません。
- sample sceneを含む旧file-browser assetは保持しました。旧assemblyの自動参照だけを無効にし、通常のViewer assemblyは現行file-browserを参照します。
- scene、prefab、ProjectSettings、Packagesの保存差分はありません。
- 全managed／Editor assemblyをUnity 2022.3.62f3でコンパイルし、batch import終了コード0を確認しました。
- 実 `LiveTimelineControl.AlterLateUpdate` と4クラスの結合をD3D11で検証し、**42/42チェックPASS**。Built-in／URP各7更新、provider計16回です。
- key選択と使用ratio、immutable snapshot、k/j独立、MultiCameraNo、camera交換、購読handlerの例外分離、登録所有権、Unknown／他pass時の出力保持、解除後の停止、source buffer保持、所有RT解放を確認しました。

人工geometryと明示済み描画値によるEditor検証です。実楽曲の自然な再生、原作camera帰属、native物理、全frame一致を証明しません。検証環境ではnative物理DLLが利用できず、復元や保護設定の変更、native物理関数の呼出しは行っていません。

実行方法と基盤修正の境界は [INTEGRATION.md](Tools/OriginalLiveRendering/INTEGRATION.md) を参照してください。以前の171チェック、35チェック、原作実測照合は独立した成果として保持します。本家へのPRは作成していません。

## 自然ライブへ向けた静的接続確認

[既存ロード経路の監査](Tools/OriginalLiveRendering/NATURAL_LIVE_PATH.md) を追加しました。キャラクタ構築時のnative物理初期化は既存の物理OFFでは省略されず、評価済み描画値の自然providerも未接続です。また、bridge通知はDirectorのmain camera更新より前なので、下地cameraの選択方法には同一updateの切替を考慮する必要があります。

これはソース監査の結果です。追加run・追加テスト・実装変更はなく、自然ライブ受入へ格上げしません。原作ゲームと隔離DLLは操作していません。

## 同一updateの下地camera選択をGPU検証

上記の静的監査後、同じ検証ブランチのUnity 2022.3.62f3／D3D11で **94/94チェックPASS**。従来の結合42件を再実行し、camera選択の52件を追加しました。Built-in／URP14それぞれ、従来7更新とcamera選択7更新です。

- 実 `Director.InitializeTimeline` によるcamera登録と切替イベント購読を使用しました。DirectorのhandlerやControlをstubへ置換していません。
- 実 `AlterLateUpdate` 内の切替通知からControlのcameraを選び、同じ呼出しで下地のGPU画像が切替先の色になることを確認しました。Control index 0／1／2と明示free cameraの4ケースを両pipelineで確認しています。
- 切替時、bridge内の `MainCameraTransform` は前のcameraを指し、その後に実 `UpdateMainCamera` を呼ぶと切替先を指しました。静的監査の順序を人工fixtureで確認できました。
- camera未通知、範囲外index、free camera未指定では、既存の `MissingSelectedCamera` と出力保持を確認しました。既存ライブへ新しい拒否条件を加えたものではありません。
- 追加はEditor検証と文書だけです。描画9クラス・2shader、Director、Control、通知順序、scene、ProjectSettings、Packagesは変更していません。

試験の選択closureは、通知されたindexと既存Control参照を使う明示登録の利用例です。通常ライブへ自動接続する実装ではありません。実Directorの通常 `Update` 全体や自然楽曲、character animation、post effect、native物理の受入も未実施です。この人工geometryの選択試験では、後段への新しい描画通知を必要としませんでした。

詳細は [結合検証](Tools/OriginalLiveRendering/INTEGRATION.md) と [camera選択の結果](Tools/OriginalLiveRendering/NATURAL_LIVE_PATH.md#同一control更新でのcamera選択検証) を参照してください。過去の原作実測、35／171チェック、Unknownと判定flagは維持します。

## cueから描画値への追加接続

自然providerの不足を一括で推測せず、保存命令で確認できた有効thicknessの分岐を既存producer mathとresolverへ接続しました。選択済みcueのkindとraw thicknessを使用し、固定値・下限・必要なreceiver分岐は明示入力です。評価済みsnapshotとproviderの評価済み値が優先され、既存の `Inputs` constructorでは追加計算を有効にしません。

Unity 2022.3.62f3／D3D11で **152/152 PASS**。従来94件を再実行し、厚さ算術12件と実Controlからのprovider結合46件を追加しました。Built-in／URP14で各16回の追加更新、provider計64回です。

- 選択・補間済みthickness、更新後のkey、明示下限、固定値側、明示receiver分岐、評価済み値優先の各ケースを描画しました。
- 両pipelineの各6ケースで、64×32出力のRGBA全画素が従来の評価済み入力経路と一致しました。raw値と有効値を別々に保持しています。
- shader角度も既存算術へ選択済みrotation sourceと人工の明示依存を渡しました。未取得の実角度定数を同定した結果ではありません。fadeとmappingも明示的な人工入力です。
- kind 0の時間依存分岐、下限、fade、角度依存が未取得の場合は、該当Unresolvedと出力保持を確認しました。
- scalar比較は別途、実SSE MAXSSとの人工81組でbit一致しました。GPUチェック数や原作実測数へ合算しません。

変更した実装は `OriginalCompositeProducerMath` と `OriginalLiveDrawInputResolver` の追加経路だけです。renderer、coverage、shader、adapter、bridge、camera producer、registration、Director、Control、通常ライブの登録と通知順序は保持しています。成功済みfeatureブランチと研究Viewerも無変更です。

これは確認済み算術を自然cueの通知先へ結ぶ段階の **TestOnly** 成果です。固定値・下限の実定数、kind 0の実状態、自然fadeと回転helper、TextureMappingの自然供給、native物理を含む自然ライブは未確定・未検証です。原作の追加採取は行っていません。

## thickness実定数の保存資料確認

`b456e57` 後の文書限定調査です。[定数の根拠と不足範囲](Tools/OriginalLiveRendering/THICKNESS_CONSTANT_EVIDENCE.md) を記録しました。参照命令から2個のfloat32を特定し、関連する保存記録10件を照合しましたが、確認範囲には対象値のbytesが収録されていません。有効thicknessの観測結果だけから固定値／下限値を一意に決めることもできません。

実値はUnknownのまま、既存の明示入力と152/152の過去受入を保持します。次に不足する値自体は各4bytes、合計8bytesです。実装、試験、原作run、開始条件、配備には変更がありません。今回の確認を新しいGPU受入や自然provider完成として扱いません。

## 取得した未確定producer依存の接続

固定厚さ・下限、回転定数、kind別base、duration倍率とfloor helperの参照先を限定実機採取で解決し、既存producer math／resolverへ接続しました。値・private captureは公開コードに埋め込まず、immutableな `ConfirmedProfile` を明示供給します。current keyのfade入力をSelectionから運び、既存評価値の優先と未取得clock等のUnknownを保ちます。

実値profileによる実Control→GPU結合は **175/175 PASS**。従来152＋追加23で、Built-in／URP14の全RGBAが従来の評価済み入力経路と一致しました。原作側も限定採取の正常停止・通常終了・復元を確認しています。過去のPending判定は変更しません。

これは従来の明示登録経路へproducerを接続した成果です。通常ライブの自動置換、自然楽曲通し、native物理、kind 0の動的receiver状態、Viewer型にないsecondary source、TextureMappingの自然供給、S生成cameraの帰属、全frame一致は別の残件です。`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown` を維持します。
