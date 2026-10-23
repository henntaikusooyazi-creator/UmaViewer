# 実Controlによる独立結合検証

## mask描画直後のAccumulation保存

`OriginalLiveChannelEvidence` は既存traceの `RightMaskDrawn` と `LeftMaskDrawn` で、その時点のAccumulationをそれぞれ独立RTへ `Graphics.CopyTexture` します。同じ合成RTの参照を二つ保存する方式ではなく、左描画や次の更新による再利用より前に各copyをGPUへ送ります。既存のcoverage入力・下地・最終出力と合わせ、1更新だけの6画像です。

|trace段階|保存画像|入力version|
|---|---|---|
|RightInputUpdated|right-coverage.private.png|1|
|RightMaskDrawn|right-accumulation.private.png|1|
|LeftInputUpdated|left-coverage.private.png|2|
|LeftMaskDrawn|left-accumulation.private.png|2|
|BasePrepared|base.private.png|2|
|FinalCompositeDrawn|two-channel.private.png|2|

receiptの各captureにselected serial、submission serial、処理段階、入力version、元／copyのRT ID、descriptor項目（寸法・format・graphicsFormat・sRGB・depth・MSAA・mip・dimension等）、copy時のsRGBWriteを保持します。現在のactiveColorSpaceも記録し、画像書込後にPNG SHA256をreceiptへ結びます。descriptorは実際のViewer RTの情報であり、原作TextureMappingや色空間の証明ではありません。

独立した既存compositorによるreplayで、最終出力に加えて左右それぞれのmask描画後Accumulationも全RGBA比較します。人工入力では、capture後に元RTを別更新でclear・再利用した後も、保存PNGとcapture metadataが保持されることを検証します。runtime renderer・provider・通常UIの処理は変更しません。

追加20件（各pipeline 10件）を含む公開人工入力は **358/358 PASS**、保存実測を含む結合検証は **1840/1840 PASS**。同じAccumulation元IDから異なるcopy IDへの保存、全6画像の同じselected serial／submissionと入力version、source／copy descriptor項目、sRGB、PNG SHA256とdecode寸法、後続clear後の画像・metadata保持を検証しました。

実楽曲の自然終端・Viewer復帰まで確認し、同じ更新の左右描画後Accumulationと最終出力が、それぞれreplayの対応段階と全RGBA一致しました。保存した左描画後RTをM、下地をD、最終出力をOとして、旧原作サンプルで観測した `O.rgb = round((M.rgb*M.a + D.rgb*(255-M.a))/255)` を保存byte空間で独立に再計算し、全保存画素のRGBと下地alpha保持を確認しました。これは処理段階・演算の対応であり、別再生の原作画像との画素一致ではありません。

原作j=0／j=1の保存画像と対応する段階は、coverage入力より後のAccumulationです。coverage入力はメモリ上のARGBFloat copyでreplayしますが、PNG exportはRGBA32 readback（Texture2Dのlinear=true）です。activeColorSpace、RTのsRGB、readbackのlinear設定は別の記録であり、linear-lightや原作と同じ色空間を証明するものではありません。PNGからfloat精度を復元したとは扱いません。原作とViewerの同一frame、camera正式帰属、自然mapping、後段FlipScreen以降の画素一致は別の未確定点です。

## 二channelの自然再生と同一更新の証拠

公開人工入力は **338/338 PASS**、保存した私有入力を含む試験は **1820/1820 PASS**。追加26件では、明示したinactive-zero policy、Unknownの保持、有効kind 0／別pass／未取得fadeの保留、同一更新のcamera・時刻・shared input version、全RGBA replay、三つのcameraのRGB寄与をBuilt-in／URP14で検証しました。alphaの異なる下地を使い、alpha差が可視寄与へ混ざらないことも確認しています。

実楽曲の二channel検証は `Invoke-NaturalPlaybackValidation.ps1` へ明示した `-RightTrack` と `-LeftTrack` を追加します。既存の単一track引数はそのままです。役割と順序は呼出し側が指定し、camera名・画面の見た目・kindからcamera帰属を推測しません。実loaderで得た二つのauthored bindingから、同じowner更新のSelectionとcameraを利用します。

同時有効になった区間から、1frameだけViewer所有の右coverage入力を左入力への更新前にコピーし、左coverage入力、下地、最終出力も同じselected serialで保存します。二つの入力は同じshared RTのversion 1／2として扱い、後から同じRTを2回読んで済ませません。camera参照がそれぞれのSelectionと一致し、下地resolverとも一致することを検査します。

保存した同一frameの入力を再合成し、最終出力の全RGBA一致を要求します。さらに右、左、下地を一つずつ除いた場合のRGB差と、各coverageが単独で占める領域・下地だけの領域を調べ、三つの可視寄与を個別に確認します。RGBA replayとRGB寄与は別の判定です。

Unity f3／D3D11の実楽曲で、自然終端・Viewer復帰までこの二channel経路を確認しました。全authored keyの選択を観測し、初回late-onlyを除くowner更新をすべて描画、同時有効な各更新もすべて左右を描画しました。上記の同一frame replay・三camera参照・共通owner pair・RGB寄与・三領域の受入も成立しました。この楽曲の有効keyはduration 0であるため、正duration fadeを二channel受入の条件にはしていません。同じ最終sourceで、従来の単一channel楽曲は正durationのfade中間値を含め自然終端・復帰まで回帰PASSです。両再生ともowner値欠落・前後結合不一致・provider fault・描画fault・実行時例外は0でした。

この受入はViewer生成RTのfull-viewport policyとnative clothなしの明示previewです。原作側のmapping、S生成cameraの正式帰属、cast、時刻原点、音声epoch、原作3分割の最終画素一致へ拡張しません。raw画像・時刻・key値・receiptは公開しません。各false／Unknownと過去のPending記録も維持します。

## 自然な連続供給とViewer再生

公開人工入力は **312/312 PASS**、確認済みprofile・保存owner clock・保存cue・保存fadeを明示する試験は **1794/1794 PASS** です。前回から追加した64件は、初回late-only、owner値の使い切り、無効key通知、pause／seek、所有権、consumer例外分離、mapping／camera不足時の全RGBA保持、単一／二channelのGPU接続、物理初期化の明示opt-outを含みます。Built-in／URP14の各32件で、private値を必要としません。

連続providerは実Controlの選択結果だけを受け取ります。正常なowner更新後も時刻が欠ける場合は異常として扱い、初回のowner未更新late-onlyとは区別します。snapshotにViewerFrameをowner比較値として代入する処理はありません。

`Invoke-NaturalPlaybackValidation.ps1 -UnityExecutable <Unity.exe> -OutputPath <repo外> -MusicId <明示楽曲> -ConfirmedProducerProfile <外部profile>` は、専用Editorでローカルの単一track楽曲を選択してPlayMode受入を行います。通常のscene・builder・asset manager・楽曲loader・Directorを使い、モデル・舞台・音声を読み込みます。無関係なicon／全楽曲UIの列挙と不足assetのdownloadは省きます。castは検証器の明示選択であり、原作cast再現ではありません。

追加loader callbackは実 `InitializeTimeline` 後・Play前に連続providerを登録します。実音声の開始後は既存Directorの通常deltaTime更新を使い、手動frame設定・seek・timescale加速を行いません。通常の楽曲終端とlive scene終了・Viewer復帰まで待ちます。native物理は初期化前に明示opt-outし、DLL復元や仮実装は行いません。

Unity f3／D3D11で、選択したローカル実楽曲の自然再生を通常の楽曲終端・scene復帰まで確認し、`NaturalViewerPlaybackCompleted` で受け入れました。全authored keyの切替と、正durationのfade中間値を継続生成し、初回late-onlyを除く各owner更新を描画しました。owner値の欠落・前後の結合不一致・provider fault・実行時例外はいずれも0です。原作の追加起動・ARM・採取は不要でした。

描画は実Viewer cameraを独立所有cameraへコピーする既存producerを使用します。full-viewport・mipなしのViewer RT設定と、明示した単一channel＋透明な第2channelを使います。source cameraのpost effect／stackは複製せず、原作の自然TextureMapping・S生成cameraや原作画素の証明にはしません。検証器はrepo外へ有限の集計・sample・preview画像を保存し、公開repoへ実値や画像を含めません。

従来の2引数loaderに自動登録や新しい成功条件は追加しません。今回の自然再生は選択したローカル楽曲の追加previewに限定し、native cloth、全楽曲・全kind、原作からの時刻stream、時刻原点・音声epoch、原作最終画素の全面一致へ一般化しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

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

## 保存した実楽曲cueからの角度生成

任意引数 `-ConfirmedNaturalCue <公開repo外の明示JSON>` を追加しました。選択・補間に必要な実楽曲のtyped cue、authored curve、明示k/jと、独立して保存したreceiver値を渡し、実Controlの選択結果からprofileでshader角度を生成します。入力は2track・各64key以下・256観測以下・256KiB以下に限定し、この検証ではcurve補間の曲線を必須・非空とします。原作値や楽曲データは公開repoへ含めません。

今回の外部入力では **1524/1524 PASS**。従来228件と追加1296件を含みます。保存184観測のraw 8成分・passを再照合し、生成角度の2成分も全件でreceiverのfloat32 bitに一致しました。Built-in／URP14では、生成角度と独立した実測角度の経路が各64×32全RGBAで一致し、画像が単色だけでないことも確認しました。cameraのgeometry設定、描画元の色、TextureMappingは人工入力です。fadeはGPU比較時だけ保存された評価済み値を明示し、owner時刻を別runから結合していません。

保存された自然cueのFだけではowner比較値・fade・TextureMappingを生成せず、未指定時の描画保留を確認しています。対象の保存key一覧には正のdurationCountを持つ有効keyがなく、自然な時間遷移をこの楽曲データから証明できません。追加runは行わず、次の採取には、別途確認した自然keyの有効値・正のdurationCount・選択ownerとの対応が必要です。

角度の観測元runの元Pending判定は変更しません。保存された限定観測の値との比較であり、同runの停止成功、通常ライブの自然再生、最終画素の原作一致を示すものではありません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 正のdurationを持つ自然keyのfade

別楽曲の限定調査で見つかった、有効・nonzero kind・正のdurationCountを持つkeyを原作で自然再生し、同じkey・ownerに属する時刻2値、比較整数、setter入力・readbackを採取しました。期間内15点と終点1点のfadeが、確認済みproducer演算とfloat32 bit一致しました。開始値ゼロの直接採取や、すべてのkind・下降fadeまで確認した結果ではありません。

`-ConfirmedNaturalFade <公開repo外の明示JSON>` は、実typed cueと独立したowner観測を受け取る追加の検証入力です。single-track、64key以下・128観測以下・128KiB以下に限定し、無効key・zero durationの観測は受け入れません。通常のproducer mathへこの拒否条件を追加したわけではありません。

実 `AlterUpdate` に2つの保存owner時刻を与え、実key選択 → 同じSelection → resolver → coverage → compositorを確認しました。**1730/1730 PASS**、従来1524＋追加206です。16点すべてでkey状態・比較整数・生成fadeが保存実測と一致し、Built-in／URP14では各点の64×32全RGBAが、独立したsetter入力を明示する経路と一致しました。最初と最後のGPU画像が変化すること、mapping不足とclock解除時の出力保持も確認しています。

このsingle SelectionのGPU試験には人工の描画色・geometry設定・TextureMappingと、明示的に透明な第2channelを使います。第2の自然trackや原作cameraを推定していません。通常ライブへの自動登録、音声epoch、原作最終画面への画素一致は未成立です。原作runは今回独立して正常停止・通常終了・所有接続復元を確認し、過去runのPendingは保持します。

私有引数を省略した公開試験は **248/248 PASS**。従来198＋人工の正duration入力50件で、summaryの `naturalFadeSource` を `SyntheticPositiveFadeInputs` とします。私有の自然観測を明示した場合は `ExplicitLocalNaturalKeyAndOwnerObservations` と区別します。zero durationの拒否例も確認しました。原作のkey・owner値、asset、native採取コード・receiptは公開repoへ含めません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。
