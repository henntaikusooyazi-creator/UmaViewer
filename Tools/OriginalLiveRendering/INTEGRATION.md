# 実Controlによる独立結合検証

検証ブランチ：`codex/validate-original-live-integration`。成功済みの `feature/original-live-rendering`（`781ef89`）へ基盤修正を混ぜないための別ブランチです。本家PRを提出するものではありません。

## 基盤修正

- [PR #234](https://github.com/katboi01/UmaViewer/pull/234) の最初の [コンパイル修正](https://github.com/OmniTroid/UmaViewer/commit/fbaaa925ab54c9f1f472a8ee7428b7e6ebc7fbff) だけを出典付きで適用しています。後続のplatform対応やshader変更は取り込みません。
- 旧file-browserのsample sceneとassetは削除せず、旧 `NewAssembly` の `autoReferenced` をfalseにして現行file-browserとの参照を分離しました。
- SliderControlの既存GBK文字列は同じ文字を示すUnicodeエスケープへ置換しました。その他のUTF-8不正ソース8ファイルはGBK往復一致を確認してUTF-8へ変換しました。処理のリファクタは行いません。
- scene、prefab、ProjectSettings、Packagesの保存内容は保持します。検証終了時のUnity生成差分も公開変更に含めません。

## 実行

Unity 2022.3.62f3で検証ブランチをコンパイルし、`Invoke-IntegrationValidation.ps1 -UnityExecutable <Unity.exe> -OutputPath <repo外の結果先>` を実行します。専用batch Editorで `OriginalRenderingIntegrationValidation.Run` を呼び、終了後にGraphicsSettings・QualitySettings・ShaderGraphSettingsの試験前のバイト列を復元します。既存のローカル編集も保存したままです。native DLLの復元や実行中プロセスの強制終了は行いません。

実ControlはForkの `umamusume` assemblyから読み込みます。Controlや選択helperをstubへ置換しません。typed keyとgeometryは人工データで、原作の非公開asset・capture・shader binaryは不要です。既存sceneファイルを保存せず、専用batch内の一時sceneで実行します。対話中のEditorや研究用Viewerで実行しないでください。

従来の結合検証はBuilt-inとURP14で各7回の実 `AlterLateUpdate` を呼びます。追加のcamera選択検証も各7回で、合計は各14回です。frame値は人工fixtureから供給するため、実楽曲の自然な時刻供給とは区別します。評価済みfade・有効thickness・shader角度・TextureMappingも明示的な人工入力です。未確定helperを推測実装しません。

従来42チェックの対象は、9クラスを通した描画、選択結果・ratio、k/j、登録所有権、camera交換、出力の左右中央、Unknown／他pass時の保持、handlerの例外分離、source cameraとbuffer、解除と所有資源解放です。

追加52チェックでは実 `Director.InitializeTimeline` を使い、通常のindex対応と実際のDirector handlerを登録します。reflectionは人工sceneの参照設定と、後段の実 `UpdateMainCamera` 呼出しに限ります。通常のDirector `Update` 全体を実行したという意味ではありません。選択closureは `OnUpdateCameraSwitcher` のindexを保持し、同一Control更新の `GetCamera(index).camera` または明示free cameraを返します。

通常camera 3台とfree cameraへの切替を両pipelineで描画し、中央の人工geometryの色が選択先と完全一致すること、bridge時点のDirector main参照が旧cameraのままであること、後段の実main更新で追い付くことを確認します。未通知・範囲外・free camera未指定時の出力保持も対象です。rendererや既存の通知順序は変更しません。

Unity 2022.3.62f3／D3D11で **94/94 PASS（従来42＋追加52）**。出力 `integration-summary.json` は `checks=94`、`baselineChecks=42`、`cameraSelectionChecks=52` と、各選択先・描画時のDirector参照・描画色を別に記録します。従来の `builtinFrames`／`urpFrames` は各7のままで、追加分は `cameraSelectionBuiltinFrames`／`cameraSelectionUrpFrames` 各7です。

これは通常ライブ全体、native物理、原作の画素一致を認定する試験ではありません。native物理DLLが利用できない検証環境でも、該当関数を呼ばない描画経路だけを確認しています。過去の原作採取・GPU replay・35チェックは別の証拠です。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。原作S生成cameraと自然helperのUnknown、過去のPendingManualRecoveryを維持します。
