> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 原作VS/PS0からのcoverage生成

`OriginalMaskCoverageGenerator` は、保存された `MultiCameraComposite` のVSとPS0の演算を移植した、明示呼出し型の生成器です。既存 `OriginalMultiCameraRenderer` と `CompositeThree` は利用できます。

## 入力と計算

`Parameters` はimmutableな入力スナップショットです。

- `Transform`：原作の確定した4成分。xyはUVへの平行移動、z±wは角度の生成に使用します。
- `TextureScaleOffset`：原作の `_MainTex_ST` に対応。
- `TextureTexelSizeY`：負の場合だけ、ST変換後・Transform加算前にYを反転します。
- `Thickness` / `Fade`：PS0へ渡す値をそのまま指定します。
- `MeshUvScaleOffset`：フルスクリーンquadと入力mesh UVの明示的な対応。省略時はidentityです。
- `MipBias`：入力texture sample用の値。省略時は0です。未取得の原作値を推測して埋める意味ではありません。

UVはmesh UV変換→ST→条件付きY反転→Transform.xyの順です。境界値はVSで計算し、PSへ補間します。

```text
q± = (u.x - 0.5) sin(τ (z ± w))
   + (u.y - 0.5) cos(τ (z ± w)) + 0.5
τ = 保存VSのfloat32定数
h = 0.5 Thickness + 0.5
q = min(q+, q-)
mask = q >= h ? 1
     : (Thickness - max(0, min(h - q, Thickness))) / Thickness
alpha = mask × inclusiveUVBounds(u) × Fade
```

角度0ではq=u.yです。sin/cosを入れ替えたり、ViewerのAnchor/Slope/Featherへ変換したりしません。入力textureのalphaは使わず、RGBだけをsampleします。UV範囲の境界0/1は含みます。FadeへのsaturateやThicknessへのepsilonは追加していません。Thickness=0の除算は原式のままで、未観測の退化条件を安定化したという主張はしません。PS1の境界線色処理は対象外です。

## 順次レンダラとの接続

生成結果は、合成前のalphaを8bitへ丸めないため、LinearのARGBFloat RTです。`GenerateSubmission` は `StraightColorWithCoverage` のsubmissionを返します。

```csharp
using Gallop.Live.Rendering;
using UnityEngine;

// shader参照はInspector等から明示的に保持する。
using (var coverage = new OriginalMaskCoverageGenerator(coverageShader))
using (var renderer = new OriginalMultiCameraRenderer(compositeShader, RenderTextureFormat.ARGBFloat))
{
    renderer.RenderSequential(baseImage,
        () => coverage.GenerateSubmission(rightCameraImage, width, height, rightParameters),
        () => coverage.GenerateSubmission(leftCameraImage, width, height, leftParameters),
        output);
}
```

同じ生成器の出力RTを、右の入力消費後に左用へ更新できます。`RenderSequential` の各producer内で生成してください。あらかじめ同じ生成器で左右を生成してから2つのtexture参照を保存すると、両方とも最後の内容を参照するため、別々のsnapshotにはなりません。

rendererの既存constructorは従来どおりARGB32です。保存中間画像の `CapturedPostBlendLayer` 再生には、その既定経路を維持してください。float共有入力を明示指定した場合も、蓄積・最終合成は従来のARGB32です。

出力は生成器が所有し、同じ解像度なら再利用します。`GenerationSerial` と `LastParameters` はCPU側の生成投入情報です。GPU完了時刻や原作frameの証拠ではありません。利用終了時は `Dispose()` で解放します。

入力textureのsample設定と色空間は呼出し側が指定します。自動camera選択、cueのバイナリ解析、jとtrack ordinalの同一視、後段のFlipScreenや通常ライブへの自動登録は行いません。

## 検証範囲

Unity D3D11で、独立した数式との比較、ST・Y反転・UV clipping・fade・非ゼロw・入力alpha無視、同じRTを使う右→左→FinalCompositeを検証しました。保存済み同一runのmask値とUV入力から生成したcoverageも、実測alphaと照合しています。少数のサンプルに1/255差が残るため、元shaderとのbit単位の同一性は主張しません。

従来の保存RGBA replayは引き続き全サンプル一致しています。通常ライブでの自然cueやcameraの帰属は未接続です。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。
