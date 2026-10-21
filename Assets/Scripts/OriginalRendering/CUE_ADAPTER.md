> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# 原作Composite cueのtyped adapter

`OriginalCompositeCueAdapter` は、選択済みkeyまたは評価済みproducer値から、immutableな `OriginalMaskCoverageGenerator.Parameters` を作る別経路の機能です。既存シーンや通常ライブに自動登録しません。

## 選択・補間

`Context` はtrack ordinal `k`、Composite選択index `j`、key token、update token、観測Unity frameを独立して保持します。不明ならnullableのまま渡します。tokenとframeはmetadataであり、adapterはそれらからkey選択や時刻換算をしません。`k` から `j` を推定せず、`j` からcameraや画面の左右を選びません。

`CueValues` は値のsnapshotです。`FromSelectedKeys` では、次keyが存在し、呼出し側から与えられた補間predicateがtrueの場合だけ、次を計算します。

```text
L = current + (next - current) * ratio
```

ratioをclampしません。thickness、色4成分、antialiasing、offset2成分、未変換のrotation source / secondary-angle sourceをこの式で扱います。kindとpassは現在keyから保持します。次keyや補間predicateが無い場合は現在値を保持します。必要な次key成分が未取得なら、補間後もその成分はUnknownです。色のgamma変換は行いません。

`FromEvaluatedProducer` は、すでに評価された値をそのまま受け取ります。補間したふりをしたり、未取得のkeyやtrackを生成したりしません。

## 未確定のhelperを混ぜない

`DrawState` は、評価済みfade、有効material thickness、shaderに渡すz/wを明示的に受け取ります。

- rotation sourceの単位、kindからbaseを得る処理、変換定数は推測しません。`maskRoll / 360` 等への自動変換はありません。
- keyのfadeTimeを固定FPSへ変換しません。元fade helperとsetterを通った値を呼出し側から渡します。再clampも行いません。
- producerのthicknessと、callbackの条件分岐後にmaterialへ渡る有効thicknessを区別します。raw値からの暗黙fallbackはありません。

ST、TexelSize.y、mesh UV変換、mip biasも `TextureMapping` として明示します。

`Snapshot.TryCreatePass0Parameters` は、未取得値を `Unresolved` flagsで返します。これは新機能のデータ状態であり、既存採取や通常ライブの開始条件ではありません。

passは元のint32と、command consumerへ渡るsigned low16を両方保持します。PS0用Parametersを作るのはwire pass=0の場合です。pass1や-1の全pass要求を勝手にPS0へ置き換えず、`OtherPassRequired` として元値を残します。未取得passも0とみなしません。

## 使用例

```csharp
var snapshot = OriginalCompositeCueAdapter.FromSelectedKeys(
    context, currentValues, nextValues, nextInterpolationPredicate, ratio,
    evaluatedDrawState, textureMapping);

if (snapshot.TryCreatePass0Parameters(out var parameters, out var unresolved))
{
    // RenderSequentialの該当producer内で実行する。
    var submission = coverage.GenerateSubmission(explicitCameraImage, width, height, parameters);
}
else
{
    // 新経路の未取得値を記録する。既存経路の受入を変更しない。
}
```

左右のproducerの接続と呼出し順は、既存 `OriginalMultiCameraRenderer` の明示的なAPIで管理します。adapterはtextureやcameraを選びません。`WithDrawState` は後から解決した値を持つ新snapshotを返し、旧snapshotを変更しません。

## 検証範囲

分離Unity D3D11で、補間分岐、範囲外ratio、kind/pass保持、Unknown伝播、metadata保持、signed low16 passを検証しました。保存済みreceiver入力から作ったParametersと既存成功入力のbit一致、およびcoverage→順次合成のGPU結果の一致も確認しています。

key選択helper、ratioの時間依存helper、fade時間計算、回転変換helperを完全移植したという意味ではありません。通常ライブ・自然cameraの接続も未実装です。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。
