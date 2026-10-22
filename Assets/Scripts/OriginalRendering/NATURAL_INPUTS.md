> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 自然なtimeline更新と描画入力の接続

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
