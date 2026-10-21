# 原作方式ライブレンダラの未解決事項

独立検証ブランチ `codex/validate-original-live-integration` の補足です。公開済み描画ブランチ `781ef89` は変更していません。

## 本家起点のコンパイル問題

起点 `07f82e9fa08f23c1cda3a9be3045a09372eae8bb` を使用しています。

- Unity 2022.3.62f3でForkをimportすると、未変更の `Assets/Scripts/LiveToolBox/SliderControl.cs:35` に `CS1010`／`CS1002` が出ます。今回の追加ソースによるエラーとは区別します。
- root側と新しいサブフォルダ側に重複ソースが存在します。[本家PR #234](https://github.com/katboi01/UmaViewer/pull/234) はこの問題等を扱っています。確認時点ではopen／未マージです。構文エラー解消後の全コンパイルが通ると主張しません。
- このブランチはPR #234を一括適用せず、既存重複ファイルを削除しません。新しいControl接続はPR #234で残される `Cutt/LiveTimeLine/LiveTimelineControl.cs` に配置しています。
- 本家全体の修正・更新後に、実Controlを通したForkのUnity受入を実施する必要があります。

検証ブランチでは、PR #234の最初のコンパイル修正単位と文字コード修正により、managed／Editor全体のコンパイルと、人工入力による実Control結合42チェックが通りました。上記は公開済み描画ブランチの起点問題として残します。

**BREAKING RISK（基盤修正の通常ライブ採用）:** 検証ブランチは重複型を整理し、現行サブフォルダ側を採用しています。通常ライブ全体の動作互換は未検証です。公開済み7コミットを保持する経路が非破壊の代替です。基盤修正を描画機能へ自動マージしません。sample scene／prefabの変更はなく、削除側script GUIDへの3参照は残存する同一GUIDのscriptへ解決しています。

検証環境では本家同梱のnative物理DLLがセキュリティ製品に隔離されています。検知の真偽をこの検証で判断せず、復元や除外設定を行いません。今回の描画テストはnative物理を呼びません。通常ライブ・native物理を含む実行受入は未完了です。

## 未確定・未実装

- Resolve元Sを実際に描いたcameraの正式帰属：`Unknown`。
- 自然fadeのduration生成、kind別の回転helper・未取得定数：未確定。raw値から単位や固定FPSを推測しません。
- fade、有効thickness、shader角度、TextureMappingの自然な供給元：未接続部分あり。resolverへ確認済み値を明示供給できます。
- 有効thicknessの確定分岐はmath／resolverへ接続済みですが、固定値・下限の実定数とkind 0の時間依存状態は未確定です。人工GPU入力での152チェックを、自然provider全体の完成と扱いません。既存constructorは追加計算を有効にせず、評価済み値が優先されます。
- 固定値・下限の参照先を保存命令から再確認しましたが、限定確認した保存記録10件には対象の2個のfloat32が未収録です。実値を有効thicknessから逆算して埋めません。[保存証拠の範囲](Tools/OriginalLiveRendering/THICKNESS_CONSTANT_EVIDENCE.md) と最小の不足情報を記録しています。
- 同一更新の下地camera選択：実切替イベントとControl参照を使う明示closureをBuilt-in／URP14の人工GPU試験で確認済み。Director main参照はbridgeより後に更新されるため単純なgetter参照は避けます。通常ライブでの描画準備完了や自動登録まで検証した結果ではありません。[検証範囲](Tools/OriginalLiveRendering/NATURAL_LIVE_PATH.md#同一control更新でのcamera選択検証) を参照してください。
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
