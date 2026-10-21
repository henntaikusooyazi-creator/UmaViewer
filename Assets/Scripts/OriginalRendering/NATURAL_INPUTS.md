> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 自然なtimeline更新と描画入力の接続

`OriginalLiveDrawInputResolver` は、既存 `OriginalLiveTimelineRegistration` / `OriginalLiveRenderSession` のresolve delegateへ渡す追加機能です。Control、bridge、camera、描画先、通常ライブの登録処理は変更しません。

## 入力を供給する場所

|入力|現在の供給元|resolverの扱い|
|---|---|---|
|track ordinal / Composite index|登録済みbridgeのSelection|kとjを別々に保持|
|現在／次key、key frame|Controlの選択結果を保持したSelection|再探索しない|
|使用ratio|Controlが実際に使ったratioを保持したCue|補間helperやlerpを再実行しない|
|thickness、色、AA、offset、rotation source、kind、raw pass|選択／補間済みのCue.Values|書き換えない|
|現在のViewer frame|Selection.ViewerFrame|metadataとして呼出し側へ渡す|
|更新識別子／key token|Selection.Serial / Cue.Source|保持。原作frameや時刻へ換算しない|
|選択camera|Selection.ViewerCamera|そのままproviderへ渡す。cameraを検索・変更しない|
|評価済みfade・有効thickness・shader角度|明示的なInputs.EvaluatedDraw|確認済みの値だけ供給する|
|ST、UV変換、TexelSize.y、mip bias|明示的なInputs.Texture|cameraや解像度から推定しない|
|kind別base・回転定数|明示的なInputs.Angles|未解決ならnullableのまま|
|duration解決後のfade計算結果|明示的なInputs.ResolvedFade|時間換算を済ませた結果だけ供給する|

rawの有効フラグとfadeTimeはViewerのkey型／Control内にありますが、現在のSelectionはその値を運びません。未解決の時間helperを埋めるためにkeyを再検索したり、反射で抜き取ったりしません。現時点の接続には評価済みfadeを利用できます。

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
3. 明示的に解決された依存を使うproducer math。

各成分を独立して扱います。たとえばfadeだけ解決しても、角度や有効thicknessを補完しません。raw thicknessを有効thicknessへ代入したり、maskRollを固定定数で変換したり、未取得fadeを1にしたりしません。

値が足りない場合は、従来の `TryCreatePass0Parameters` が同じUnresolvedを返します。新しい開始条件や成功条件を追加しません。kindやpassからcameraを選ばず、pass要求も書き換えません。providerの例外は既存session/bridgeの例外処理へそのまま伝えます。

## 確認範囲

CLIで、実Viewerのmanaged bridgeのBeginFrame→CaptureSelected→FrameReadyからresolverを呼び、Parameters生成まで確認しました。型付きfixtureによる接続確認であり、Control.AlterLateUpdate、camera描画、Unity GPU、元Viewerの自然ライブを今回実行した結果ではありません。従来のGPU受入結果は維持します。

復旧後の確認点は、明示登録した楽曲の自然なControl更新がこのdelegateへ届くこと、指定したcamera・描画先を使うこと、描画結果です。未解決helperやS生成cameraを解明するための新runを、この接続の前提条件にはしていません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。
