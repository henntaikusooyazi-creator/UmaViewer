> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 自然なtimeline更新と描画入力の接続

## 二channelの順序と無効区間

再生検証wrapperの `-RightTrack <明示ordinal> -LeftTrack <別ordinal>` で二channelを指定できます。各bindingは実trackのauthored Composite番号から作り、ordinalから番号を推測しません。引数を省略した単一track経路は保持します。同時有効区間の有無と全keyの状態は、ロードした実timelineを基に確認します。

`OriginalLiveContinuousRenderer` の追加overloadの最後へ `transparentInactiveChannels: true` を渡すと、無効key、解決済みfadeが厳密に0、wire pass 0の組だけを透明submissionにします。必要ないcoverageを生成せず、未知の厚さを仮定値へ置き換えません。SnapshotとLastUnresolvedにはUnknownを残し、`LastTransparentChannels` が省略したchannelを示します。既存overloadはfalseです。

同時有効時は、両Selectionのowner値、camera、coverageが必要です。片側が未解決なら出力を保持する従来の規則を保ちます。kind 0の動的receiver状態、別pass、原作の未知mappingをこのpolicyで解決したことにはしません。

`TraceEmitted` はViewer所有RTへの任意の観測点です。実際の右入力更新→右mask→左入力更新→左mask→下地→最終合成と、同じFrame・camera参照を受け取れます。source cameraや原作プロセスにhookを追加せず、observerなしの既存呼出しも保持します。公開Editor検証器は1frameに限定してGPU入力をコピーし、全RGBA replayとRGB寄与、左右／下地それぞれのcoverage領域を確認します。画像・owner値・instance IDは外部のprivate結果先だけへ保存します。

追加の限定観測では、RightMaskDrawn／LeftMaskDrawnのAccumulationを各描画直後に別RTへcopyします。再利用される合成RTの同じ参照を保持せず、左右それぞれのpost-blend画像を既存coverage・下地・最終画像と同じselected serial／submissionへ結びます。descriptor、sRGB、入力versionとPNG hashを併記し、replayの同じ段階でも全RGBA一致を検証します。原作のmask描画後画像と対応するのはこちらの段階です。保存PNGはRGBA32 readback（Texture2Dのlinear=true）であり、activeColorSpaceやRTのsRGBとは別の設定です。coverageのfloat値の厳密保存や、linear-light・原作の色空間・mappingの証明へ拡張しません。

## 連続供給の明示登録

`OriginalLiveContinuousProvider` は、ロード済みControl、確認済みprofile、authored trackと一致する明示bindingを受け取ります。Controlの実 `AlterUpdate`／`AlterLateUpdate` をそのまま利用し、保存した時刻配列やViewerFrameからの推測時刻は供給しません。providerの `FrameReady` には同じ更新のimmutableなSelectionと解決済みSnapshotが届きます。

無効keyも通知するため、enable解除後も以前の有効keyを使い続けません。比較時刻の組は1回のlate frameで消費し、初回、owner更新なしの再通知、clock解除後はUnknownです。停止中の同時刻やseek後の逆向き時刻も実owner値のまま渡します。登録済みの別bridgeは置換せず、Disposeは自身の登録だけを解除します。

既存楽曲loaderの追加overload `LoadLive(live, cast, initializeNativePhysics, onTimelineReady)` は、実 `InitializeTimeline` 後、music初期化・Play前にcallbackを呼びます。ここでproviderを接続すれば、通常Directorの全更新を受け取れます。従来の2引数loaderは物理初期化あり・callbackなしのままです。

`OriginalLiveContinuousRenderer` は、このproviderのSnapshotを既存coverage・順次compositorへ渡す追加のViewer previewです。呼出し側が下地cameraのresolver、順序を持つtrack、shader、描画先を指定します。単一channelは `leftTrack=null` と明示し、第2channelを透明にします。存在しない自然trackを推測しません。二channelも明示bindingで利用できます。

`FullViewportMapping(height)` は、Viewer自身が生成するfull-viewport、identity UV/ST、mipなしRTに対する明示設定です。source viewportが対応しない場合は保持し、原作の未確定TextureMappingやcamera正式帰属へは転用しません。値・cameraが欠ける場合も従来どおり画素を保持します。

利用側はrenderer→providerの順にDisposeし、caller所有のoutputを別途解放します。profileは公開ソースへ埋め込まず外部から与えます。native物理を省く場合も、明示loader引数で初期化前にopt-outします。

公開人工入力312件と保存実測を含む1794件はGPU結合試験です。ローカル実楽曲の自然再生は別のPlayMode受入として、通常の終端・scene復帰、実音声進行、連続owner値、key切替、描画を確認します。原作からViewerへのclock契約や、原作最終画素一致は別の未確定事項です。

`OriginalLiveDrawInputResolver` は、既存 `OriginalLiveTimelineRegistration` / `OriginalLiveRenderSession` のresolve delegateへ渡す追加機能です。Controlの選択順、camera、描画先、通常ライブの登録処理を維持し、明示登録時にowner更新の2値を観測できます。

## 入力を供給する場所

|入力|現在の供給元|resolverの扱い|
|---|---|---|
|track ordinal / Composite index|登録済みbridgeのSelection|kとjを別々に保持|
|現在／次key、key frame|Controlの選択結果を保持したSelection|再探索しない|
|使用ratio|Controlが実際に使ったratioを保持したCue|補間helperやlerpを再実行しない|
|thickness、色、AA、offset、rotation source、kind、raw pass|選択／補間済みのCue.Values|書き換えない|
|現在のViewer frame|Selection.ViewerFrame|metadataとして呼出し側へ渡す|
|owner時刻から解決した比較整数2値|明示opt-in後のSelection.ComparisonClock|同じowner更新で取得した組だけを利用する|
|更新識別子／key token|Selection.Serial / Cue.Source|保持。原作frameや時刻へ換算しない|
|選択camera|Selection.ViewerCamera|そのままproviderへ渡す。cameraを検索・変更しない|
|評価済みfade・有効thickness・shader角度|明示的なInputs.EvaluatedDraw|確認済みの値だけ供給する|
|ST、UV変換、TexelSize.y、mip bias|明示的なInputs.Texture|cameraや解像度から推定しない|
|kind別base・回転定数|明示的なInputs.Angles|未解決ならnullableのまま|
|duration解決後のfade計算結果|明示的なInputs.ResolvedFade|時間換算を済ませた結果だけ供給する|
|有効thicknessの分岐依存|5引数constructorのInputs.Thickness|明示した固定値／下限と、確認済みkind分岐または明示receiver分岐を使う|

選択済みcurrent keyの有効フラグとfadeTimeは、Selectionの `KeyEnabled` / `RawFadeDuration` に保持します。keyの再検索や反射による抽出は行いません。評価済みfadeを供給する既存経路も保持します。

Viewer frame、bridge更新serial、原作のcue比較値F、観測Unity frame、QPC、録画時刻は別の情報です。resolverは相互の変換を行いません。

## 既存登録APIへの接続

```csharp
OriginalLiveTimelineRegistration RegisterKnownInputs(
    Gallop.Live.Cutt.LiveTimelineControl control,
    OriginalLiveTimelineRegistration.TrackBinding right,
    OriginalLiveTimelineRegistration.TrackBinding left,
    System.Func<OriginalLiveTimelineBridge.Frame, UnityEngine.Camera> selectBaseCamera,
    UnityEngine.Shader compositeShader,
    UnityEngine.Shader coverageShader,
    UnityEngine.RenderTexture output,
    System.Func<OriginalLiveTimelineBridge.Selection,
        OriginalLiveDrawInputResolver.Inputs> confirmedInputsForSelection)
{
    var resolver = new OriginalLiveDrawInputResolver(confirmedInputsForSelection);
    return new OriginalLiveTimelineRegistration(control, right, left,
        selectBaseCamera, compositeShader, coverageShader, output, resolver.Resolve);
}
```

これは既存APIの呼出し例です。新しい自動登録箇所やシーン変更は追加していません。right/left、下地camera、描画先は呼出し側が明示します。元のresolve delegateやconstructorもそのまま利用できます。

providerは各Selectionについて1回呼ばれます。更新時点のSelectionを直接受け取り、resolver内で前frameの入力をキャッシュしません。camera交換後も既存sessionはその更新のSelection.ViewerCameraを使います。元cameraやControlへ書き込む機能はありません。

## 値の優先順

1. 選択snapshotに既に保持されている評価済み値。
2. providerから明示された評価済み値とtexture mapping。
3. 明示的に解決された依存を使うproducer math。有効thicknessは5引数constructorで依存を指定した場合だけ、選択済みcueと結びます。

各成分を独立して扱います。たとえばfadeだけ解決しても、角度や有効thicknessを補完しません。raw thicknessを有効thicknessへ代入したり、maskRollを固定定数で変換したり、未取得fadeを1にしたりしません。

## cueから有効thicknessへの追加経路

確認済みの分岐を使う場合は、次を各Selectionに対するproviderから返せます。

```csharp
return new OriginalLiveDrawInputResolver.Inputs(
    confirmedEvaluatedDraw, confirmedTextureMapping,
    resolvedAngleInputs, resolvedFade,
    new OriginalCompositeProducerMath.ThicknessInputs(
        confirmedFixedValue, confirmedMinimum, observedReceiverBranch));
```

固定値・下限・receiver分岐はnullableな明示入力です。分岐が未指定なら、kind 1の固定値側と、その他のnonzero kindのraw／下限比較側だけを使います。kind 0の時間依存分岐は推測しません。詳細は [純粋計算](PRODUCER_MATH.md#有効thickness) を参照してください。

既存の2～4引数constructorではこの追加計算を有効にしません。選択keyの再探索・再補間、時刻換算、前updateの値の再利用はありません。raw thicknessと評価済みmaterial値を別々に保持したまま、その更新のcueから計算できます。

fade、角度のhelper依存、TextureMappingをこの追加によって自動生成できたとは扱いません。別effectのstage fade、material、RTサイズからの推測や、coverageの `LastParameters` を入力へ戻す経路も追加していません。

値が足りない場合は、従来の `TryCreatePass0Parameters` が同じUnresolvedを返します。新しい開始条件や成功条件を追加しません。kindやpassからcameraを選ばず、pass要求も書き換えません。providerの例外は既存session/bridgeの例外処理へそのまま伝えます。

## 確認範囲

CLIで、実Viewerのmanaged bridgeのBeginFrame→CaptureSelected→FrameReadyからresolverを呼び、Parameters生成まで確認しました。型付きfixtureによる接続確認であり、Control.AlterLateUpdate、camera描画、Unity GPU、元Viewerの自然ライブを今回実行した結果ではありません。従来のGPU受入結果は維持します。

復旧後の確認点は、明示登録した楽曲の自然なControl更新がこのdelegateへ届くこと、指定したcamera・描画先を使うこと、描画結果です。未解決helperやS生成cameraを解明するための新runを、この接続の前提条件にはしていません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

## 確認済みproducer依存の接続（2026-10-22）

版を照合した限定採取で、固定厚さ・下限・回転定数、kind別base、duration倍率とfloor helperの分岐を確認しました。私有の値・captureは公開ソースへ埋め込まず、呼出し側がimmutableな `OriginalCompositeProducerMath.ConfirmedProfile` を構築します。

既存providerの戻り値に `WithConfirmedProfile(profile, confirmedCueComparisonFrame, confirmedOwnerComparisonFrame, confirmedUseCueThickness)` を適用すると、そのSelectionの選択済みcueから有効thicknessとshader角度を生成します。fadeには同じcurrent keyから保持した `KeyEnabled`、`RawFadeDuration`、`CurrentKeyFrame` を使用します。next keyを再検索・再補間しません。

比較frameを両方明示した場合、または以下のowner更新から比較値の組が取得できた場合に、raw duration → float32倍率積 → double floor → int32 → fadeの既存算術へ進みます。倍率は取得した依存であり、Viewer FPSから生成しません。比較値が未取得の場合も厚さ・角度は独立して解決し、fadeを推測しません。

## owner更新からの比較値供給

`registration.Bridge.UseConfirmedOwnerClock(confirmedOwnerTimeMultiplier)` を明示した後、次の実 `LiveTimelineControl.AlterUpdate` が現在のowner時刻と更新前のowner時刻をbridgeへ渡します。確認済みのfloat32積・floor・整数変換で2値を生成し、その後に選択されたkeyと同じimmutableなSelectionへ保持します。`ViewerFrame`、Unity frame、serialを比較値へ変換しません。

providerが `WithConfirmedProfile(profile)` を返し、比較整数を両方とも省略した場合に、このSelectionの組を利用します。明示整数が片方だけなら組を混ぜず、fadeを未解決のまま保ちます。両方の明示整数、評価済みDrawState、明示ResolvedFadeの既存優先は保持します。

opt-in直後でowner更新がまだない場合と、`ClearConfirmedOwnerClock()` 後は比較値がありません。未取得時は出力を保持します。通常ライブへの自動登録や、音声時計・原作とViewerの時刻原点、seek/pauseの対応をこの経路から確定しません。`SourceToViewerClockContract=false` を維持します。

既存snapshot、providerの評価済み値、明示済み算術依存の優先を保ちます。profileなしのconstructorは従来どおりです。kind 0の未取得receiver状態、Viewer型にないsecondary source、TextureMapping、camera帰属はprofileで補完しません。通常ライブへの自動登録も追加しません。

実値profileを外部入力として実Control → resolver → coverage → GPUまで検証します。これは人工geometry・typed keyによる結合試験であり、自然楽曲の通し受入ではありません。最新件数はPROGRESSとINTEGRATIONに記載します。

## 自然な有効keyでのfade検証

別楽曲で正のdurationを持つ有効keyを自然採取し、同じkey・ownerの現在／比較時刻から生成したfadeをsetter入力・readbackと照合しました。期間内15点・終点1点を確認し、その実typed keyを実Controlで選択して既存profile resolverへ接続する追加受入も通っています。raw durationを既知の倍率・floorへ通し、ViewerFrameを比較整数の代用にはしません。

実時刻・key・receiptは公開repo外の明示検証入力です。既存 `UseConfirmedOwnerClock` / `WithConfirmedProfile` の接続を使い、通常ライブへの新しい自動登録や元cameraへの変更は追加しません。GPU側のTextureMappingと描画素材は人工入力のままです。原作とViewerの時刻原点・音声epoch、自然楽曲通し、開始ゼロや下降fade・全kindの実測は別の残件です。`SourceToViewerClockContract=false` を維持します。
