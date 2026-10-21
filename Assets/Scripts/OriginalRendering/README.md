> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 原作方式の順次合成レンダラ

`Gallop.Live.Rendering.OriginalMultiCameraRenderer` は、既存 `CompositeThree` と独立して使用する明示呼出し型のレンダラです。通常ライブや既存シーンへの自動接続はありません。

## 実装済みの順序

1. 透明な蓄積RTを準備する。
2. 右側用producerを呼び、入力を共有RTへコピーして描画する。
3. 左側用producerを呼び、同じ共有RTを更新して追加描画する。
4. 下地を出力へコピーする。
5. 蓄積映像をalpha合成する。出力のalphaは下地から保持する。

左右の描画用materialを分け、各submissionのopacityとUV変換を保持します。共有入力RTと蓄積RTは解像度が変わるまで再利用し、`Dispose()` で解放します。呼出し前のactive RTとsRGB write状態を復元します。

## 入力の意味

- `StraightColorWithCoverage`：RGBとcoverage alphaを持つ入力。RGBはSrcAlpha/OneMinusSrcAlpha、蓄積alphaはOne/Oneで加算します。
- `CapturedPostBlendLayer`：既にRGBへalphaが反映された保存中間画像の再生用。RGBへalphaを二重適用しません。元のcamera RGBを復元する機能ではありません。

coverageは呼出し側が供給します。保存VS/PS0のTransform・thickness・fadeから生成する別機能として `OriginalMaskCoverageGenerator` を追加しました。使用方法は [COVERAGE.md](COVERAGE.md) を参照してください。自動cue解釈、原作cameraの同定、自然callbackへの接続は未実装です。任意のRGB画像を渡すだけで原作の斜め境界になるわけではありません。

最終合成は蓄積RGBとalphaを用いたSrcAlpha/OneMinusSrcAlphaです。蓄積画像のalphaに対する処理は、入力段階の処理と区別してください。

## 呼出し例

```csharp
using Gallop.Live.Rendering;

// shaderはInspector等から明示的に保持する。
// Player buildでShader.Findだけに依存するとshaderが除外される場合がある。
var renderer = new OriginalMultiCameraRenderer(shader);

renderer.RenderSequential(baseImage,
    () => ProduceRightCoverageInput(),
    () => ProduceLeftCoverageInput(),
    output);

// 利用終了時
renderer.Dispose();
```

producerは `OriginalMultiCameraRenderer.Submission` を返します。右producerの入力コピーと描画の後で左producerを呼ぶため、同じ外部camera RTを順番に更新して返す構成にも使用できます。原作でどのcameraが映像を生成したかという未確定事項を、このAPIから推測しません。

完成済み入力を使う場合は `Render(baseImage, right, left, output)` を使用できます。Unityメインスレッドで呼び出し、入力・出力の有効期間は呼出し側で管理してください。所有する中間RTや出力自身を入力として返さないでください。

`Submission.ScaleOffset` は明示的なUV変換です。Y反転は自動適用しません。初期検証対象はLinearのARGB32、point samplingです。HDR、色空間変換、後段のFlipScreenやPresentは別工程です。

`Trace` はCPU側の処理・描画投入順を示します。GPU完了時刻や画面frameと同一視しないでください。`HasSubmission=false` の段階では `Draw` は適用対象外です。

## 検証範囲

分離したUnity 2022.3のD3D11プロジェクトで、保存済みの右描画後・左追加後・FinalComposite後の画像に対するGPU readback一致を確認しました。同じ入力RTをproducer間で更新する試験、通常RGB/coverageのblend試験、既存 `CompositeThree` のsmokeも実施しています。

これは保存データによる `TestOnly` の順次合成再生です。原作の全解像度・全frame・全cameraの再現や、通常ライブへの採用を意味しません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown` を維持します。既存 `CompositeThree` と既存の受入結果は変更しません。

## cue値のadapter

選択済みkeyや評価済みproducer値からcoverage入力を作る別機能として、OriginalCompositeCueAdapterを追加しました。使用方法と未確定値の扱いは [CUE_ADAPTER.md](CUE_ADAPTER.md) を参照してください。既存シーンや通常ライブへの自動登録はありません。

## Viewer timelineと明示cameraの接続

実LiveTimelineControlの選択結果を受け取るOriginalLiveTimelineBridgeと、明示的なcamera描画を順次合成へ渡すOriginalLiveRenderSessionを追加しました。既定は未登録です。使用方法、Built-in／URPの対応範囲、通常ライブ採用との区別は [LIVE_BRIDGE.md](LIVE_BRIDGE.md) を参照してください。
