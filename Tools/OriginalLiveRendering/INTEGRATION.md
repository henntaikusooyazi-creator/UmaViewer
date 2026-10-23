# 実Controlによる独立結合検証

## 描画ごとの準備と中間出力通知

`OriginalPostProcessChain.Binding(property, Func<Texture>)` は、外部textureを各drawで1回解決します。登録時にdepthがまだ存在しなくても、frame callback内で現在のdepthを返せます。null・解放済みRT・現在の出力へのaliasは描画前に拒否し、前回値へfallbackしません。従来の固定textureと前段参照も使えます。

`Process(camera, input, prepare, completed)` の `prepare(stage, material)` は各段のtexture bindingより前、`completed(stage, target)` は描画直後に同期実行します。定数変換の意味は呼出し側が明示します。callback内でcopy・readbackしないRT参照は次回再利用時に変化します。例外、再入、処理中のdisposeでは成功件数を進めず、active RTとsRGB writeを復元します。callbackが変更した呼出し側所有materialの値はrollbackしません。旧2引数のProcessはcallbackなしで同じ経路を使います。

公開試験は7段を2更新描き、現在のdepth・typed payload・段順序・各段のUNorm量子化後のRGBを独立したCPU式で確認します。欠落depth、自己alias、prepare／completedの例外、再入とdispose、状態復元も検証します。追加経路を含む自然入力試験は **105/105 PASS、4352画素**、後処理試験は **46/46 PASS**、Control結合は **376/376 PASS**。いずれもBuilt-in／URP14を含みます。

私有の保存原作入力でも全段をprepareで設定し、現在depthを明示resolverへ渡しました。独立drawの896画素、連続生成した各中間段の896画素、最終128画素がRGBA一致しました。後二者は同じ保存参照を再評価した結果で、新しい原作観測ではありません。選択variantのmetadataから追加のparameterとtexture参照を確認しましたが、metadata上の対応をtimelineからCBを生成する演算の証明へ昇格していません。特にPostFilm関連の自然更新が現在のpayloadにありません。

自然probeは人工式を7段へ適用します。各段は `color.rgb * 0.5 + (depth * 0.2, intensity / 7 * 0.1, 0.03)` で、その直後にUNormへ書きます。CPU比較は入力から各段の量子化を順に評価します。原作programの自然供給ではありません。最大32 camera入力をsampleし、各入力の元color・depthと7段の出力をreadbackする上限です。結果のmode、段数、全段の完了数、段別比較数もwrapperで検査します。

現コードでの自然再生は142秒を完走し、7372/7372 owner更新、左右同時有効58/58更新、116入力・812段、49比較・6272画素、最大RGB差1 byteを確認しました。自然終端・Viewer復帰、初回late-only 1回の保留、fault 0を含む独立した新runです。原作の時刻原点や7段の自然定数変換を確定する結果ではありません。

## 自然cameraのdepthとtimeline payload

`OriginalCameraProducer.SetInputFramePostProcessor(processor, depthCopyShader)` と連続rendererの同名APIは、colorに加えて今回のdepthを要求する明示経路です。旧 `SetInputPostProcessor` と排他的で、どちらも未登録なら従来経路を使います。`IOriginalCameraFramePostProcessor.Process` にはproducer所有の `OriginalCameraFrame` を渡します。RTは次の描画で再利用されるため、そのcallback内で消費・copyしてください。serialは保存画像の不変性を保証しません。

frameにはSource camera、color／depth RT、RenderSerial、SelectedSerial、実render cameraのview／GPU projectionとnear／far、reversed-Z情報があります。depthはlinear RFloatへのraw device値で、eye depthへ推測変換しません。Built-inではcameraのdepth生成後、URP14ではそのcameraの `endCameraRendering` 内で現在のdepth bindingをcopyします。URPのRTHandleを一時RT名としてBlitする方法は採用しません。source cameraのcomponent／depth設定は変更せず、private render cameraだけに要求を付け、callback・command bufferは描画のfinallyで解除します。別cameraのglobal depthを描画終了後に検索する経路はありません。APIの位置付けは [CameraEvent](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.CameraEvent.html) と [BuiltinRenderTextureType.Depth](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Rendering.BuiltinRenderTextureType.Depth.html) を参照してください。対応範囲は実検証したBuilt-in／URP14です。

Controlの既存Bloom／Diffusion通知の直前に、発行するstructを `OriginalLiveTimelineBridge.Frame.PostEffect` へcopyします。BeginFrameでクリアするため、通知がなければnullです。再選択・再補間や既存handler順序の変更は行いません。`OriginalLiveContinuousRenderer.RenderingFrame` は同期描画中だけ有効です。`OriginalLivePostProcessInputs` にその取得関数と明示処理delegateを渡すと、SelectedSerial、一致するselected cameraとowner観測、作成済みdepth、typed payloadを要求したうえで処理します。値から原作CBへの変換はdelegateの責任であり、このclassはshader layoutを推測しません。

追加の公開人工試験は、実Controlのpayloadを使ったbridge保持、欠落時のUnknown、異なるserialの拒否と、上下でdepthが異なる人工geometry、複数更新のnear／farを検証しました。Built-in／URP14合計 **45/45 PASS、768画素** です。投影から計算したdepthと実depthが一致し、depthとcolorの上下関係も確認しました。source cameraの状態、中央下地の迂回と解除も検証しています。既存の公開postprocess **32/32**、Control結合 **376/376** は独立した再検証です。

`Invoke-IntegrationValidation.ps1 -NaturalPostProcessOnly` はこのfull Viewer人工試験を実行します。`Invoke-NaturalPlaybackValidation.ps1 -FrameInputs` は、通常の自然再生検証へ明示的な人工RGB probeを追加します。通常UIの描画には自動登録しません。初版の単一段probeは `color.rgb * 0.5 + (depth * 0.2, intensity * 0.1, 0.03)` を使用しました。現在の7段版とreadback上限は上節を参照してください。どちらも係数は人工値であり、原作後処理の演算・設定を表しません。

今回の「うまぴょい伝説」は142秒を完走し、7851 owner更新を7851回描画、左右同時有効63/63更新、初回late-only 1回の保留、楽曲終了後のViewer復帰、物理初期化の既存opt-outを確認しました。126 input renderに同じ更新のdepthとpayloadを供給し、typed payloadの変化2回、非far depthを確認しています。7回・896画素の人工RGB比較は最大差1 byte、renderer／provider faultは0でした。旧72更新や65更新の別再生結果を書換えず、可変frame数だけで非退行を判断しません。

depthの初稿shaderでMainTexのproperty宣言を欠いたためdefault textureを読んだ点、URPのRTHandleを一時RTとして扱った点は、この限定試験で検出して修正しました。非対称geometryの検証では既存materialのpipeline適合性を混ぜない専用の人工color／depth fixtureを使います。上下の深度差・投影との照合を受入条件へ残しました。

自然再生の後に、URPのdepth要求を解決済みboolではなく元の継承optionへ復元する局所修正を加えました。最終45件には継承状態の復元確認3件を含み、32件・376件も最終ソースで再実行しました。142秒の成功runはこの復元修正前の独立した証拠として保持し、最終ソースでの自然再生再実行までは主張しません。

今回成立したのはViewerの自然入力供給と、その明示処理経路の検証です。原作の7段に必要な全自然定数／variantの生成、自然TextureMapping、中央下地cameraの原作帰属、時刻原点、原作と同じframeのRGB一致、全画面・FlipScreen以降は未確定です。最後のshaderを後から候補再生成の参照へ結ぶ案は、今回のWARP／hardware閉鎖ホストでは同一候補でもcanonical IUnknownが異なり、identityの根拠にならないため採用しませんでした。原作deviceで再生成を実測した結果ではありません。新規原作runは0回で、旧生成API照合の不足を解消したとは主張しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 明示した後処理chainの接続とRGB比較

`IOriginalCameraPostProcessor.Process(Camera, RenderTexture)` は、その回のViewer camera出力を受け取る追加の明示経路です。`OriginalCameraProducer.SetInputPostProcessor`、または `OriginalLiveContinuousRenderer.SetInputPostProcessor` へ登録します。左右inputの描画後に呼ばれ、下地には適用しません。返却RTは作成済みでcamera入力と同じ大きさである必要があります。`null` の登録で解除できます。producerは登録先の所有権を持ちません。呼出し側が解除後にdisposeしてください。

汎用実装 `OriginalPostProcessChain` は1〜16 passを受け付けます。各 `Pass` にはMaterial、shader pass、出力サイズ、任意のMeshとtexture bindingを明示します。Meshを与えると保存頂点による描画、与えないとBlitを使用します。`Binding.Stage` は `-1` が今回のcamera入力、`-2` が明示external texture、0以上が先行passの出力です。先方参照・自己参照、解放済み入力、再入、所有出力と入力のaliasを拒否します。中間RTはlinear ARGB32で、shader・sampler・投影・色空間の解釈は呼出し側の責任です。Material／Mesh／external textureは呼出し側が所有し、chainは中間RTだけを解放します。登録だけで原作shader、camera、depth、clockとの対応は成立しません。

公開人工試験は、実camera出力→サイズの異なる中間RT→複数入力の最終passをBuilt-in／URP14で実行しました。中間UNorm書込みを含む人工式とのRGB比較、alpha、中央下地の迂回、解除、fault／再入、RT／sRGB状態復元、caller所有物の保持を確認し、**32/32 PASS、4096画素** です。人工RGB式の許容差は最大1 byteであり、以下の原作保存入力の完全一致とは別判定です。既存の公開結合試験も今回 **376/376 PASS** になりました。

私有の追加採取では、7 draw全てについて以下を同じrun・context・描画区間へ接続しました。

- VS／PSの束縛buffer全体と、独立した従来snapshotとの一致。
- 実draw-helper内の頂点宣言、実IA layout参照、indexed引数、vertex／index buffer。POSITIONとUVは実宣言から解釈し、strideから推測しません。
- 入力textureの原寸・format・samplerと限定領域。depthの実float入力も保存。
- draw直後の整数画素領域。GPU copyをその場で発行し、Mapは観測済みPresent後に行います。

同じbuildの私有program候補を、元命令のbit表現、保存全定数、実頂点とUV、sampler、元解像度のviewportで公開chainへ渡しました。readbackのrow／channel配置を人工gradientで独立に較正し、画素合わせのためのY反転・epsilon・丸め補正は加えていません。各drawの保存入力からの **896/896 RGBA一致**、前段の生成結果を接続した最後の **128/128 RGBA一致** を得ました。非一様・非ゼロの参照領域です。sample footprintの保存範囲内判定と、前段の無効領域を伝播する別GPU描画でも、比較領域の無効sampleは0でした。明示stage graphと独立保持した中間RT列も一致しています。

この比較はBuilt-in D3D11の限定領域です。最後の書戻し候補は、この入力・定数・頂点に対する動作が一致しましたが、実shader生成APIとの同一run identityは未確定です。候補の静的定数参照の不足は解消しましたが、全variantや未観測状態までは保証しません。入力の保存領域外は未検証で、原作shader・HLSL由来コード・実定数・vertex・画像・native採取器は私有です。通常のViewer camera RGB、自然なdepth／program／定数の連続供給、音声epoch、左右と中央全体の原作画素一致は別の未完了事項として保持します。

最終runはStopCompletedと保持handleによる復旧、hook復元・drain・pin照合、正常ゲーム終了後の所有配備復元まで確認しました。最終採取器の閉鎖試験は **877/877 PASS**。重複する早期shader採取を外した構成なので、以前の1006件からの差を件数だけで非退行判定しません。途中版はdeferred入力未完了、setter quotaによるcallback欠落、実頂点宣言数に対する読取quota不足を個別に保存し、callback枠の分離、最大宣言を含む境界試験、全入力完了を要求する終了述語へ修正しました。最終7/7入力が揃った後にGPU比較へ進めています。旧Pendingは変更しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 実shader生成との同一run照合

限定した候補集合について、原作のshader生成APIへ渡されたbytecodeを全byte比較しました。生成APIの正常終了、返却objectの取得、そのobjectの保持、実drawにおける同じobject参照、生成がdrawより先であることを照合しています。保持により、解放後のobjectアドレス再利用を同一shaderと誤認することを防ぎます。候補の長さ・名前・別runの参照だけでは採用しません。

| 対象 | 同一runの照合結果 |
| --- | --- |
| 先頭の拡散2 draw | VS／PSを実生成bytecodeへ接続。正常終了runの証拠 |
| 続くbloom3 draw | VS／PSを実生成bytecodeへ接続。正常終了runの証拠 |
| 複数入力の拡散draw | 追加Pending runの保存データでVS／PSを接続。停止成功とは別判定 |
| 最後の入力RT書戻しdraw | VS／PSともUnknown。静的copy候補を自動採用しない |

正常終了runでは10/14 stage、追加Pending runのデータ照合では12/14 stageです。保存された生成記録が欠落・未完了・失敗、返却objectやstageが不一致、生成時刻がdraw以後、異なるbytecodeへの重複一致の場合はUnknownとします。候補集合外、hook開始前に生成済みのshaderも解決したとは扱いません。同一のcompiled bytecodeに複数のserialized pass／keyword別名がある場合、bytecode内容を同定できても別名の選択は一意にはなりません。

7 drawについて同じevent・context・VS／PSに属するVS定数の限定prefixを保存し、複数入力drawのdepth viewとresourceに対応する限定gridも採取しました。depthはGPU側で保存用にコピーし、観測したPresentの完了後に読み戻しています。depthの全画素や再描画一致を証明するものではありません。

照合済みbytecodeだけをローカルで逆アセンブルし、参照する静的定数registerが実保存範囲に含まれるかを検査しました。確認済みPSの参照は保存範囲に収まります。VSには保存prefixの外にある変換・描画定数の参照が残るため、prefix取得を「VSの全入力充足」とは扱いません。頂点buffer・input layout・補間、textureのfilter参照近傍も未取得です。限定grid同士を使ったRGB replayや、定数欠落の推測補完は行っていません。

追加run用の閉鎖検証は1006/1006、関連受信側は36/36がPASSです。実機の追加runは全体読取上限に達し、左右消費drawの完了を要求する早期終了条件も成立せず、元のPendingManualRecoveryを保持しています。先行3 runのStopCompletedをこのrunへ流用しません。ゲームを通常終了し、配備時hashと原本hashを照合して所有DLL・設定・所有metadataを復元しました。これは実行中のcontroller保持handleによる復旧成功とは別の結果です。

次の再描画入力は、未同定の書戻しshader、確認済みVSが実際に参照する不足定数、各drawの頂点・補間とtexture参照近傍です。採取器の読取予算も含めてまとめて資格化する必要があり、同条件の再runを繰り返すだけでは充足しません。camera全writer帰属、queue位置、自然TextureMapping、MipBias、owner／音声epochと最終RGB一致は独立して未確定です。

実bytecode・定数・depth・採取器・receiptはローカル専用です。公開変更はこの説明と進捗のみで、Viewer runtime・shader・通常UIを変更していません。既存GPU／自然再生は今回再実行しておらず、過去の成功件数へ加算しません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## Resolveから右maskまでの描画内容

新しい限定runは内部owner時刻で開始し、対象Resolveから実際の右mask API呼出しまでの描画を保存しました。途中RTのwriterも対象に含め、従来のresource watch集合は変更していません。保存した観測対象のDraw／DrawIndexedは7件で、消費maskを区間終端として別に保持しています。以前の「4回」は共有入力RTへ直接関わる観測であり、後処理全体のpass数を確定したものではありません。

| 区間内の順序 | 観測内容 |
| --- | --- |
| 1–2 | 拡散系のshader名を持つ描画と途中RT |
| 3–5 | bloom系のshader名を持つ描画と途中RT |
| 6 | 拡散系のshader名、depthを含む複数texture参照 |
| 7 | 入力RTへの書戻し。保存gridで入力・出力RGBAが一致 |

各描画で実際のVS／PS参照と名前、texture参照・descriptor、pixel shader定数の限定範囲、sampler、viewport、blend、入力と出力の描画前後gridを保存しました。GPU copyを各描画の前後に発行し、Mapは観測したPresentの完了後に行います。RTの次の再利用が保存内容を上書きしない条件です。元の整数画素中心を保持し、解像度が違うgrid同士は直接の画素一致と扱いません。

対象Resolve、順序付きAPI記録、同じcontext、実際の右mask呼出しを含む時間範囲、入力RTへの最後の書戻しを照合しました。各描画の保存gridでRGB変化を確認し、書戻しから消費までに対象入力への別の観測済みwriteがないことも確認しています。これは限定したAPI・画素範囲の結果です。全画素の演算一致、全writerの帰属、shader名からのbytecode同定は未証明です。

描画キュー位置は取得できず、各記録の該当診断をUnknownとして保持しています。画素取得の成否とは別判定であり、今回の7 drawを既存のcamera帰属証拠へ自動的に結合しません。保存された拡散系・bloom系shader候補では、pixel shaderが参照する定数範囲を今回の保存でカバーできました。ただし、実際のbytecode／variant、頂点側の変換・補間、depth画素は不足しています。既存の `OriginalCameraProducer` はcamera scripts／effect componentsを複製しないpreviewで、この後処理列の自然なRGB供給は未接続です。

採取条件をCPU側の選択フラグへ依存させた初期版は対象を取り逃がしました。GPUの消費開始をCPUの選択時刻とみなす診断も採用せず、区間内保存と後段のAPI照合へ分け、選択が途中で変わる閉鎖試験を追加しています。別の途中版では診断fieldを既存の厳格なqualification schemaへ追加して受信拒否されました。診断値を独立sidecarへ移し、既存schemaを保持する保存receipt replayを追加しました。初期slotから対象時刻までの変化も記録し、ロード用の有限待機をARM前に置いてから、元の前後一致条件を満たす新しいrunを実行しました。待機時間をcueや音声epochの代用にはしません。

最終runは元controllerでStopCompleted／保持handleによる復旧、正常worker終了、hook復元・drain・pin照合、通常ゲーム終了後の所有配備復元が成立しました。採取器の閉鎖検証は **796/796 PASS**、関連受信側は **57/57 PASS** で、変更した受信moduleの分岐計測は100%です。以前のrunのPendingは変更しません。

公開差分は説明文書のみです。原作値・shader binary・native採取コード・receiptはローカル専用に保持します。Viewerの376/376・1868/1868、自然再生の成功は今回再実行せず、別の成果として保持します。自然TextureMapping、owner epoch、中央下地camera、後段表示処理、原作対Viewerの同一frame・最終画素一致と各false判定は未確定のままです。

## 内部時刻と歌詞時刻表による同期

Viewerの既存loaderは楽曲の歌詞時刻表をミリ秒から秒へ読み込み、`Director` は同じライブ時刻をtimelineの `AlterUpdate` と歌詞の `UpdateLyrics` へ渡しています。歌詞本文を画面から認識する経路は不要です。選択した原作データでも時刻表の存在と時刻の非減少を確認しました。本文・時刻表・実値は公開repoへ含めません。

原作の限定採取では、字幕画像による開始条件を、内部ownerとcue比較値による条件へ置き換えました。clockだけを確認する有限待機から、手前の内部checkpointで描画観測を準備し、その後も同じownerを待って対象到達時に採取受付を開始します。owner変更・逆行・対象超過・上限・timeoutを拒否し、選択receiverの配列帰属、実owner比較値とcueの一致、setter前後の安定性を確認します。準備完了が対象より前であることと、対象から採取受付への移行も独立に検証します。Viewer frameや壁時計から原作時刻を推測していません。

実機で内部条件による開始、左右の消費draw、元controllerによる正常停止・hook復元・drain・pin照合・保持handle復旧を確認しました。これは原作の歌詞表示clock、音声epoch、最終表示frame、原作対Viewerの時刻原点の証明ではありません。既存Viewerの歌詞処理やrendererは変更していません。各false／Unknown、過去のPendingを維持します。

別の後処理追加runは、記録が1件に留まり、内部slotの前後一致も不成立でした。hook解除・drain・個別pin照合が成立しても、元controllerのPendingは維持します。4後処理のshader／binding／画素変換を一式取得した結果にはしません。追加観測では任意PNG本体を省き、既存の保存上限内に数値記録の容量を確保しています。採取器の閉鎖検証783件と受信側66件はPASSですが、Viewerの既存376件・1868件へ加算せず、実機の未達を置き換えません。

## 右Resolve前writerのcamera帰属と後処理

原作の限定runで、右Resolve前に保存したwriter4件が同じqueue／論理範囲の一意なrender-taskへ一致しました。taskの保持contextとCullResults側contextの一致、完了順序、camera IDを検査し、同じrunの右receiverへ接続しました。左右の自然reader・消費drawも同じ実更新で取得しています。限定された保存writerの帰属であり、全描画や最終mask入力全体のcamera帰属ではありません。

同じrunの右receiverと解決済みRTを先行更新で確認し、最初の次更新だけを対象にしました。準備段階のResolveやPresentを対象へ混ぜず、別更新へ観測枠を延長しません。実測順序はcallback→task→reader→Resolve→mask drawであり、task完了を保存readerの開始と比較します。同じResolveが二つのcamera記録へ添付される重複は除きます。

当時はResolve後から右mask drawまでに、共有入力RTへ直接関わる4 drawと、別RTを経由した入力RTへの書戻しを観測しました。後続の区間採取では途中RT上の描画も含めて7 drawを保存しています。保存されたsource／destination参照には、対象Resolveから中間RT・書戻し・右mask入力へ至る経路があります。全writerのcamera帰属や、各入力の全画素への寄与は未証明です。**Resolve版を無変更で消費したという旧判定はfalseのまま保持**します。後続の左Resolveは別versionであり、右側の経路を継承させません。resource経路の検査には無関係な上書き・clear・未完了による経路切断の負例も含めました。

最初の未ARM起動は元のPendingを保持しています。その後、通常再生で字幕の黒縁maskの背景依存を再現しました。追加の目視確認画像との共通部分へ限定し、白文字mask・一致基準・待機時間を変えず、別の通常再生で二つの字幕遷移と時刻区間の整合を確認しました。旧失敗のスコアを後から復元した結果ではありません。

途中の採取で、追加したcamera IDの読取り位置とcallback前にtask完了を要求した条件が誤りと判明しました。保存済みlayout・camera ID・task時刻で訂正し、隣接した別値、reader欠落・重複・task完了前のreaderを拒否する試験を追加しました。既存のread／byte／row／hook／期限／停止条件は維持しています。最終native閉鎖検証は共通592件＋右用38件の630/630、字幕36件・補助24件もPASSです。以前の625件構成やwire26件、Viewerの376/376・1868/1868とは独立した検証です。

最終runはStopCompleted、hook復元・drain・pin照合・保持handleによる復旧、通常ゲーム終了後の所有配備復元が成立しました。UI画像保存は既存容量上限で終了し、native成功とは別判定です。途中runや古いPendingを遡って成功扱いしません。

既存Viewerの右・左それぞれの `Selection.ViewerCamera` 使用方針に、右Resolve前writerでも構造上の対応が得られました。後処理を省略して原作RGBと一致するという意味ではありません。公開差分は文書のみで、採取コード・実値・raw・receiptはローカル専用です。Viewerのbuild／GPU／自然再生は今回再実行していません。最終mask入力の全writer／後処理camera、中央下地、自然TextureMapping・投影・MipBias・owner epochはUnknownです。各false判定と以前の左限定帰属を維持します。

## 左入力のcamera帰属と共有RTのversion

原作の限定観測では、保存した左入力のresource writerとrender-taskの描画キュー参照・論理範囲を照合し、重なるtaskが一つだけであることを要求しました。task側の保持contextとCullResults側のcontextの一致、非ゼロのcamera ID、正常完了、writerより前のtask完了、Resolveより前のwriter完了も確認しています。camera IDはrenderer数とは別に扱い、同じrunのcallback・receiver・camera参照へ接続しました。

この保存範囲では左receiverのcameraと一致しました。保存したwriterは有限の一部分です。入力画像の全writer、右channel、中央下地の生成cameraまで確定した意味ではありません。別runのpointerやcamera IDを再利用しません。

共有RTのAPI identityが同じでも、内容のversionは異なります。同じResolveが複数のcamera記録に添付される場合は重複を除き、Resolveの後でその入力を消費するdrawだけへ帰属させます。今回の対象Resolveは右drawの後・左drawの前です。左の結果を先行する右drawへ遡って適用しません。

`OriginalLiveContinuousRenderer` は各channelの `Selection.ViewerCamera` と明示した下地resolverを使用します。この既存方針に対して、左の限定観測との構造上の対応が得られました。原作cameraの投影・post effect・RGB一致や、下地resolverの正式帰属を認定するものではありません。`FullViewportMapping` は引き続きViewerが生成するRTのpolicyです。自然なUV/ST生成元・投影契約・MipBiasの不足を、このcamera帰属から補いません。

今回の公開差分は文書のみで、runtime renderer・provider・coverage shader・Editor検証器は変更していません。直前の公開376/376・private1868/1868は再実行せず、過去の独立した成功として保持します。原作の最終限定runの正常停止・復元と、容量上限に達したUI画像保存は別判定です。実値・raw・private receipt・採取コードは公開しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と、過去runの元Pending判定を維持します。

## 保存drawのmask alpha比較

`Invoke-IntegrationValidation.ps1 -ObservedMaskAlpha <repo外のJSON>` は、限定された外部alpha観測をEditorだけの比較へ渡します。通常のrenderer・provider・楽曲再生・UIは変更しません。原作画像や実値を公開repoへ保存しません。

入力は元RTの `width`／`height`、有限gridの `gridWidth`／`gridHeight`、明示した `flipGridY`、`right`／`left` の評価済みdraw、`rightAlpha`／`leftAlpha` のbyte整数配列です。各drawは `transform`、`scaleOffset`、`meshUvScaleOffset`、`texelSizeY`、`thickness`、`fade` を指定します。配列は入力gridの行順です。寸法・配列長・byte範囲・有限値を検査してからGPU資源を確保します。これは原作のraw解析器ではなく、呼出し側で同一drawへの帰属を確認した明示入力です。

各grid位置は `floor((2*i+1)*sourceSize/(2*gridSize))` の元整数画素を参照し、その中心で描画結果を読みます。`flipGridY` は入力policyであり、実測への一致が最大になる向きを自動選択しません。原作の投影matrixやcamera帰属が未取得なら、それを解決したとは扱いません。

右の比較は初期alpha 0を明示します。左は保存された右alphaをPoint textureとして既存の `CapturedPostBlendLayer` 経路で配置し、その上に生成した左coverageを描きます。左の比較は原作の右draw前の内容を推測しません。観測済みalpha加算はOne／Oneであり、RGBのSrcAlpha／InvSrcAlpha式をalphaへ適用しません。

sample用RGBとbiasは試験用の明示値です。原作MipBiasは未取得のまま保持し、RGBや色空間の一致を判定しません。異なる人工RGBA／biasでも同じalphaになることを検証します。receiptはalpha限定、右のclear policy、投影契約未成立、原作bias未取得、各false判定を記録します。

公開人工入力 **376/376 PASS**（追加18件）、保存実測を含む結合検証 **1868/1868 PASS**（追加28件）。元画素中心、明示UV/ST/Y反転、保存右alphaからの左描画、入力RGBA／bias非依存、誤ったgrid方向の検出、入力境界をBuilt-in／URP14で確認しました。保存した原作drawとの比較では右は最大1byte差、左は一致しました。新しい原作runや自然ライブの長時間再生は実施していません。原作の同一frame・camera RGB・全画素一致、自然mapping供給、owner epochは未確定です。

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

## 確認済み後処理パラメータの生成

`OriginalPostProcessProducerMath` は、確認済みCPU演算の範囲を計算します。`ConfirmedProfile` の依存値は呼出し側が供給し、原作値を公開コードへ埋め込みません。縮小寸法はfloat32積から整数へ変換した後で除算し、Bloomのaspect除算・倍率積も命令順に丸めます。出力はPixelSize、brightness／saturation／contrast／threshold、初期Bloomとblur用Bloom、weight／blend flagです。graphやshader variantの選択は計算器の責務に含めません。`SourcePrefilterParameter` も、確認済みの該当分岐を呼出し側が選んだ場合だけ使用する値です。

PostFilmは評価済みの `FilmInputs` を明示供給します。depth clipの分岐、offsetのvector化、rollのaspect成分、depth／vignette flagを生成し、他の成分を保持します。入力snapshotは変更しません。movie layer・movie maskの準備は対象外で、推定による代替計算は行いません。

`OriginalLivePostProcessParameters` は従来の `OriginalLivePostProcessInputs` の検査を使い、current selected update・camera・owner観測・color／depth・Bloom／Diffusion payloadが揃ってから計算します。任意のFilm providerは `FilmObservation` を返し、そのselected serialとcameraも照合します。nullはUnknownです。Filmが必要なprogramは呼出し側で描画を保留し、前回値やゼロ値を補いません。callbackやGPU処理が失敗した場合、成功countは増えません。

```csharp
var parameters = new OriginalLivePostProcessParameters(
    () => renderer.RenderingFrame,
    confirmedPostProcessProfile,
    (frame, values) =>
    {
        // この例のprogramはFilmを必要とする。
        if (!values.Film.HasValue)
            throw new InvalidOperationException("Current Film input required.");
        return chain.Process(frame.Source, frame.Color,
            (stage, material) => PrepareConfirmedProgram(stage, material, values),
            (stage, output) => CopySelectedIntermediate(stage, output));
    },
    frame => ResolveConfirmedCurrentFilm(frame));
renderer.SetInputFramePostProcessor(parameters, depthCopyShader);
```

上記のprofile、program、binding、Film providerは明示登録です。この例は原作shaderや自然PostFilm keyの解決を提供しません。既存API、未登録時の描画、中央下地、通常ライブの登録・通知順序は変更していません。

人工試験は既存wrapperの `-PostProcessParametersOnly` で実行できます。原作入力の任意引数や他の専用試験modeとは併用できません。Built-in／URP14で **44/44 PASS、4096画素のRGB差0**。丸め・分岐・入力保持・同一camera／serial・Unknown／古いFilm／欠落payloadの扱いを検証しました。既存Control結合は現在のソースで376/376 PASS、wrapperの併用拒否2ケースもEditorを起動せず確認しました。

私有の原作比較では、対象drawの同一更新に属するreceiver cameraとCPU入力を照合し、7段へ供給する49 float32成分をGPU定数へbit比較しました。同じ成分の複数drawへの再供給を含む件数です。公開算術でこれらを再生成して既存chainへ渡し、独立7 drawの896画素、連続7段の中間896画素、最終128画素でRGBA一致しました。連続比較は同じ保存参照の再評価です。行列・global値・画像・depth・頂点等の残りの保存入力は保持しており、全定数を自然生成した結果ではありません。最終shaderの生成identityや全画面・全frame一致も認定しません。CPU command bufferから描画queueへの完全なwriter閉包を証明するものでもありません。原作値・native layout・program・採取器・receiptはrepo外だけに保存しています。

今回の変更後に142秒の自然楽曲は再実行していません。PostFilmの連続timeline providerは未接続で、自然7段RGBの完成とは区別します。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

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
