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

## 明示依存によるcue→draw providerの追加検証

現在のrunnerは、上記94件に厚さ算術12件とprovider結合46件を加え、Unity f3／D3D11で **152/152 PASS** です。実 `AlterLateUpdate` の選択・補間結果を使い、5引数の `Inputs` から有効thicknessを計算します。producerのkind分岐とcallbackのscalar比較だけを移植し、実定数・時間helper・別effectの値を推測しません。

各pipelineで6ケースを従来の評価済み入力経路と比較し、64×32の全RGBAが一致しました。更新されたraw thicknessがその更新のGPU画像を変えること、明示receiver分岐で左右の有効値が別々になること、評価済み値を保持することも確認しています。kind 0の分岐不明、下限未取得、fade未取得、角度依存未取得の4ケースは出力保持です。

出力JSONの `thicknessMathChecks` は12、`drawProviderChecks` は46です。追加更新は `drawProviderBuiltinFrames`／`drawProviderUrpFrames` 各16、`drawProviderCalls` は64。`drawProvider` 配列には人工raw値、有効値、従来経路との画素比較結果を記録します。全体のControl更新は各pipelineで従来7＋camera選択7＋provider16です。

このproviderは固定値・下限・角度依存・fade・mappingを人工入力として供給しています。自然ライブ全体のproviderが完成した意味ではありません。別途行った実SSE MAXSSとの81組の比較は純粋算術の補助検証で、152チェックや原作実測の件数へ足しません。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。原作S生成cameraと自然helperのUnknown、過去のPendingManualRecoveryを維持します。

## 確認済みproducer profileを使う結合検証

限定実機採取で解決した依存を外部profileとして供給し、Unity 2022.3.62f3／D3D11で **175/175 PASS**。従来152件を再実行し、profile算術・実Control結合23件を追加しました。内訳はbaseline42、camera選択52、厚さ12、従来provider46、確認済みprofile23です。

`Invoke-IntegrationValidation.ps1` の任意引数 `-ConfirmedProducerProfile <明示JSON>` でローカルの確認済み依存を渡せます。省略時は公開の人工profileで検証し、どちらを使ったかはsummaryの `confirmedProfileSource` へ記録します。私有profileやcaptureはrepoへ含めません。

current keyの有効フラグ・fadeTimeをimmutableなSelectionに保持し、その同じ選択から厚さ・角度・duration/fadeをresolverへ供給しました。Built-in／URP14で各64×32の全RGBAが、独立した従来の評価済み入力経路と一致しました。clock不足時の出力保持と、評価済み入力の優先も確認しました。

初期検証で同名Math型の衝突と、Monoの高精度中間値が原作のscalar丸め順と異なる問題を検出・修正しました。float32 bit一致と倍率積・下降fadeの境界ケースを検証に保持しています。新しいepsilonやshader補正は加えていません。

wrapperはProjectSettingsに加え、隔離DLLに付属する既存metadataも実行前のbytesへ戻します。DLL本体の隔離・削除状態を変更せず、Unity importによるmetadata削除だけを持ち出さないための処理です。自然楽曲通し、native物理、S生成camera、未知clock/receiver/mappingは本受入で成立扱いにしません。

## owner比較値の接続受入

後続の検証は **228/228 PASS** です。従来175件を含み、owner比較値の投影・実Controlからの供給・入力不足時の保持など53件を追加しました。`-ConfirmedOwnerClock` へ公開repo外の明示入力を渡し、保存されたowner時刻のbit列、比較整数2値、setter入力を照合しています。省略時は人工入力を使い、summaryで出所を区別します。

実 `AlterUpdate` → owner比較値snapshot → 同じSelection → resolver → coverage → compositorを実行し、Built-in／URP14の各64×32全RGBAが独立した評価済みfadeの経路と一致しました。clockのopt-in直後、片方だけの明示整数、解除後は出力を保持します。明示した整数の組と既存評価値の優先も確認しました。

GPU試験のgeometry・key・TextureMappingは人工入力です。原作の保存比較に含まれた無効keyの終点値を、自然なfade遷移の検証へ広げません。時刻原点・音声epoch・最終画面frameとの対応、自然楽曲の通し受入は未成立です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false` を維持します。
