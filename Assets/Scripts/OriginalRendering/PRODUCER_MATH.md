> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 原作producerの純粋計算

`OriginalCompositeProducerMath` は、保存命令で確認できたfade、shader角度、有効thicknessの計算を扱う追加機能です。既存adapter、timeline、camera producer、GPU合成を変更せず、呼出し側が明示的に利用します。

## 有効thickness

`EvaluateEffectiveThickness` は、選択・補間済みの `CueValues.Thickness` と、明示した `ThicknessInputs` からmaterialへ渡すscalarを計算します。raw thicknessを常にそのまま採用する機能ではありません。

- 確認済みのproducer分岐ではkind 1が固定値側、その他のnonzero kindがraw値と下限の比較側を選びます。kindの名前や画面方向は推定しません。
- kind 0は時間helperとreceiver状態に依存します。kind未取得の場合も含め、明示された `UseCueThickness` がなければUnknownです。
- `UseCueThickness` が指定されていれば、そのreceiver分岐を優先します。falseは `FixedValue`、trueはraw thicknessと `Minimum` の大きい方です。
- 固定値・下限の実定数はこの実装へ埋め込んでいません。使用する分岐の値が未供給なら結果もnullableのままです。使わない分岐の定数は要求しません。
- 比較は `cue > minimum ? cue : minimum` で、等値・unordered時は元のscalar命令と同じく第2オペランドを採用します。符号付きゼロやNaNを別値へ正規化せず、epsilonや新しい有限値判定も追加しません。

既存resolverの5引数 `Inputs` に `ThicknessInputs` を指定したときだけ、この計算が接続されます。元snapshotの評価済みthickness、providerの評価済みthickness、今回の計算の順に優先します。既存constructorと未指定時の動作は保持しています。

これは保存された分岐の純粋計算です。後続の限定採取で得た定数は末尾のConfirmedProfile経路から明示供給します。kind 0の動的状態と自然provider全体の供給は別の未確定点です。

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

後続のConfirmedProfile接続とowner比較値の供給は、実Controlを通すUnity GPU試験でも確認しています。確認したhelper・定数の範囲と、元Viewerでの自然ライブ、S生成cameraの帰属、他楽曲の自然再生は区別します。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

## 取得したhelper依存の利用

`ConfirmedProfile` は固定厚さ・下限・回転3定数・duration倍率と、11項目のkind別baseを明示入力として保持します。tableはコピーして保持し、呼出し側の配列変更を反映しません。保存helperのunsigned範囲検査に従い、範囲外kindのbaseはゼロ、kind未指定はUnknownです。

`ResolveDurationCount` は保存されたMULSS、拡大、floor、CVTTSD2SIの順を実装します。NaN・範囲外はx64のindefinite integerを保持します。倍率からFPSを推定せず、durationの単位やclockの対応は呼出し側の明示依存のままです。

Unity/Monoはfloat局所変数を高精度のまま保持する場合があるため、保存scalar命令に対応する演算段階で実際の32bit保存を行います。倍率積と下降fadeの境界ケースを検証に保持し、epsilon補正や角度・UVの合わせ込みは行いません。`System.Math.Floor` は同名のViewer型との衝突を避けて完全修飾しています。

既存評価値の優先と、未取得入力をUnknownとして保持する契約は変更しません。S生成camera、自然clockの対応、kind 0のreceiver状態、全画素一致全体をこのprofileだけで確定しません。

## owner比較値の純粋計算

`ResolveOwnerComparisonFrames(currentOwnerTime, comparisonOwnerTime, confirmedMultiplier)` は、独立して渡された2つのowner時刻へ、保存されたfloat32積→double floor→int32変換を適用します。比較側を内部の前回値やframe counterから生成せず、逆向きseek、同じ時刻、負の値にも新しい補正を加えません。

実owner更新の保存命令と実採取値を照合し、同じ依存を使う明示bridge経路を追加しました。採取したsetter比較は無効keyの終点値であり、自然なfade遷移全体の実測ではありません。非ゼロfadeの接続は人工keyを使う実Control→GPU試験で検証し、音声原点や自然楽曲全体のclock契約は未確定のままです。
