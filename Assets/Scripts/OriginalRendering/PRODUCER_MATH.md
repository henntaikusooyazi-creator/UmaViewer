> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 原作producerの純粋計算

`OriginalCompositeProducerMath` は、保存命令で確認できたfadeの分岐とshader角度の計算を扱う追加機能です。既存adapter、timeline、camera producer、GPU合成を変更せず、呼出し側が明示的に利用します。

## 角度

`EvaluateShaderAngles` は、既存adapterで選択・補間した `CueValues` を受け取ります。

```text
z = (KindBase + RotationSource) / Divisor
kind == 10: w = (SecondaryOrigin - SecondaryAngleSource * SecondaryScale) / Divisor
それ以外:  w = 0
```

基準値・変換定数は `AngleInputs` へ明示します。単位やkindの名前、基準値の対応表を推測せず、固定の角度換算も追加しません。未取得値はnullableで保持します。`AngleResult` はz/wを独立して保持し、足りない入力を `Missing` で返します。既知のnon-10 kindなら、zが未確定でもw=0までは表せます。

## fade

`EvaluateFadeAfterResolvedDuration` は、原作の時間換算・helper・整数変換を**通過済みのint32 durationCount**を受け取ります。rawのfadeTimeをこの引数へ直接代入しません。固定FPSでの換算も行いません。durationCountが未解決なら、この関数の結果を供給せず、従来どおりfadeをUnknownまたは明示的な評価済み値として保持します。

引数は現在の比較frame、owner側の比較値、key frame、durationCount、keyの有効値、現在keyのkindです。owner比較値を「前frame」と自動解釈したり、関数内で保持・更新したりしません。

- 終点と差分のint32加減算はwrapを維持し、比較はsignedで行います。
- 期間内は整数をそれぞれfloatへ変換して除算し、原作の比較と演算順でfadeを計算します。
- 期間後はkeyの有効値から終点値を選びます。
- `OutFlag` と `ReturnValue` は元helperの出力を別々に保持します。
- `RequestedCameraEnabled` は計算上の操作要求です。nullは操作要求なしを表し、この機能自体はcameraを操作しません。
- setterの比較ではNaNと負のゼロをそのまま保持します。時間やdurationの補正、新しい拒否条件は追加しません。

## adapterへの適用

```csharp
var angleInputs = new OriginalCompositeProducerMath.AngleInputs(
    resolvedKindBase, resolvedDivisor, resolvedSecondaryOrigin, resolvedSecondaryScale);

// duration換算が解決済みの場合だけ計算する。未解決ならnullを渡す。
OriginalCompositeProducerMath.FadeResult? fade = resolvedFade;
var next = OriginalCompositeProducerMath.ApplyToSnapshot(snapshot, angleInputs, fade);
```

`ApplyToSnapshot` は新しいsnapshotを返します。既存の `Draw.Fade` と `Draw.ShaderAngles` があれば、その評価済み値を優先します。有効thickness、texture mapping、k/j、key token、stage、補間ratioは保持します。値が足りなければ該当成分はUnknownのままです。

## 確認範囲

CLI上の純粋計算として、分岐境界、整数wrap、float演算順、未取得依存、評価済み値の優先、snapshot保持を確認しています。保存済みsetterとの比較と、評価済みreceiver入力の保持も確認しています。

時間換算helper、kind別helperと定数を完全移植したという意味ではありません。新機能のUnity GPU受入、元Viewerでの自然ライブ、S生成cameraの帰属、他楽曲の自然再生は今回の確認対象外です。過去のGPU成功判定と採取結果は維持します。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。
