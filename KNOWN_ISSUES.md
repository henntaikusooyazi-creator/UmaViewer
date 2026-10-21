# 原作方式ライブレンダラの未解決事項

## 本家起点のコンパイル問題

起点 `07f82e9fa08f23c1cda3a9be3045a09372eae8bb` を使用しています。

- Unity 2022.3.62f3でForkをimportすると、未変更の `Assets/Scripts/LiveToolBox/SliderControl.cs:35` に `CS1010`／`CS1002` が出ます。今回の追加ソースによるエラーとは区別します。
- root側と新しいサブフォルダ側に重複ソースが存在します。[本家PR #234](https://github.com/katboi01/UmaViewer/pull/234) はこの問題等を扱っています。確認時点ではopen／未マージです。構文エラー解消後の全コンパイルが通ると主張しません。
- このブランチはPR #234を一括適用せず、既存重複ファイルを削除しません。新しいControl接続はPR #234で残される `Cutt/LiveTimeLine/LiveTimelineControl.cs` に配置しています。
- 本家全体の修正・更新後に、実Controlを通したForkのUnity受入を実施する必要があります。

## 未確定・未実装

- Resolve元Sを実際に描いたcameraの正式帰属：`Unknown`。
- 自然fadeのduration生成、kind別の回転helper・未取得定数：未確定。raw値から単位や固定FPSを推測しません。
- fade、有効thickness、shader角度、TextureMappingの自然な供給元：未接続部分あり。resolverへ確認済み値を明示供給できます。
- mask alphaの過去実測比較には9点の1/255差が残っています。補正値やepsilonは加えていません。
- PS1の境界線色、全楽曲・全frame・全解像度での再現：未完了。
- 元Viewer f1の通常ライブ受入と、通常ライブへの自動切替：未実施。自動切替は今回の対象外です。
- 最新原作F採取runの停止判定：`PendingManualRecovery`。今回の公開準備で過去の判定を書き換えません。

## 明示利用時の制約

- **BREAKING RISK（明示camera描画時）:** 追加camera描画は負荷と描画callbackを増やします。source cameraのscript、post effect、AdditionalCameraData、camera stack、custom rendererは複製しません。通常ライブを保持するにはbridgeを未登録のまま使えます。独自producerを `RenderSequential` へ渡す選択肢もあります。
- **BREAKING RISK（serialized schema）:** typed groupへの `MultiCameraNo` 追加により、JSON出力にfieldが増えます。旧データでfieldが存在しない場合の0を、原作の確定番号として使わず、明示 `TrackBinding(k,j)` を使用してください。既存描画はこのfieldを読みません。
- ARGBFloat coverageは明示的な新経路です。従来constructorのARGB32は保持します。未解決値による新経路の描画保留は、既存ライブや採取の開始条件を変えません。

## 公開・本家PR前の確認

- 確認時点の本家rootにはLICENSEファイルがなく、GitHubのlicense判定もありません。third-partyの個別LICENSEとは区別し、本家へのPR前に追加ソースのライセンス方針をmaintainerと確認する必要があります。本ブランチで本家へ新しいライセンスを付与しません。
- 公開対象は自作C#、shader、人工入力テスト、設計文書、検証概要です。原作DLL、非公開asset、DXBC、逆アセンブル、hook、raw capture、private receipt、認証情報、個人パスは含めません。
- private入力を必要とする実楽曲・原作照合テストは移植対象外です。公開人工テストの成功を原作実測の再検証へ格上げしません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown` を維持します。
