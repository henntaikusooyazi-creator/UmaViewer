# thickness実定数の保存証拠調査

更新日：2026-10-22。`b456e57` を基準とした **StaticOnly** の調査です。`FixedValue` と `Minimum` の実値はともに **Unknown** を維持します。実装変更、新しいUnity試験、原作runはありません。152/152は前回の人工入力GPU受入として保持します。

## 確認したこと

保存されたcallback命令から、固定値のloadと下限比較の参照先をそれぞれ独立に再計算しました。必要な値はfloat32が2個、合計8bytesです。

|入力|保存命令で確認した用途|実値|
|---|---|---|
|`ThicknessInputs.FixedValue`|固定値側の分岐でmaterialへ渡すscalar|Unknown|
|`ThicknessInputs.Minimum`|raw thicknessとのscalar比較の第2オペランド|Unknown|

関連するComposite、callback、shader／clock、producer依存の保存記録10件を限定確認しました。各記録のsummary hashと、byte行の長さ・hashを照合し、対象2値を含む収録範囲がないことを確認しています。別の近傍定数が保存されていても、その値をこの2入力へ割り当てません。

これは選択した保存記録の収録範囲についての結論です。全archiveの不存在を主張する調査ではありません。privateの参照先、raw値、capture、逆アセンブル、receiptは公開せず、詳細はローカル証拠に保持しています。

## 描画後の有効値からは一意に決まらない

既存のreceiver設定、material定数、GPU入力の照合は独立した成功成果です。ただし、描画に渡された有効thicknessと、その生成に使う固定値・下限値は別の情報です。

たとえば比較側で `max(raw, minimum) == raw` が成立しても、分かるのは `minimum <= raw` という条件であり、下限の一意な値ではありません。分岐が直接記録されていない場合は、固定値側との区別も別途必要です。

別run間のreceiver設定一致も、そのrunの定数参照先やreceiver分岐の直接読取へは昇格しません。保存済みreplayの有効値を、実定数の既定値としてコードへ埋め込む変更は行っていません。

## 次に必要な最小の証拠

最も直接的なのは、既存のmodule同一性へ紐づく2個のfloat32の保存値です。必要な値自体は各4bytes、合計8bytesで、helper全体や新しいrendererは必要ありません。

同じcallback／commandの実分岐とscalar書込みが既に結ばれた記録があれば、固定値側の出力から `FixedValue` を、比較側で有効値がrawを上回った結果から `Minimum` を確認する方法もあります。今回確認した資料からは、この代替証拠も成立扱いにしません。

これは不足情報の特定です。追加run・メモリ読取・hook・配備切替を実施または自動要求する計画ではありません。既存の開始条件や採取条件にも追加しません。

## 既存経路

- 既存snapshot／providerの評価済みthicknessが優先されます。
- 追加算術を使う場合は、確認済みの固定値・下限値だけを明示供給します。未取得値はnullableのままです。
- kind 0の時間依存receiver分岐は別の未確定事項として保持します。既存のnonzero kindや評価済み入力を止めません。
- 自然fade、回転helper依存、TextureMapping、native物理を含む自然ライブ、S生成cameraの正式帰属は今回の調査では変更していません。

文書差分と変更範囲のみを検証します。C#、shader、Director、Control、scene、ProjectSettings、Packages、研究Viewer、過去runの証拠を変更していないため、build／GPU／過去受入の再実行は行いません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`、過去の `PendingManualRecovery` を維持します。
