# 実Controlによる独立結合検証

## CPU Filmとowner値の限定接続（2026-10-26）

通常Addでは原作owner値を実Controlへ明示供給し、補間後FilmとCPU producerの消費24成分、限定128画素のRGBA一致を確認しました。inverse VignetteAddは既存観測の消費17成分でlayerの値の対応を一意化し、CPU Filmによる128画素比較を再実行しました。inverseの未採取owner値は補完していません。

既存のControl・producer math・selector・registry・chainを私有検証で使用しました。通常ライブのclock、camera選択、mask mappingを置換した変更ではありません。[NATURAL_INPUT_JOIN.md](NATURAL_INPUT_JOIN.md) に、未使用laneの差、観測GPU入力を保持した範囲、静的に確認したmask／時刻経路と残る検証条件を記録しています。

## 追加2候補の実行選択と原作入力比較（2026-10-26）

通常Add（実pass 5）とinverse VignetteAdd（実pass 6）を、別々の原作runで実描画へ対応付けました。現在keyword、runtime候補表の選択program／holder、全bytecodeが一致する生成PS objectとGPU消費PSを、同一run・一意なqueue範囲で照合しています。候補表は保存資産の全行・順序を照合し、要求keywordと選択候補の宣言keywordは別の集合として扱います。両対象とも選択候補に `COLOR_ONLY` を含みますが、それを実際の有効keywordへ読み替えません。

私有比較器から既存の公開selector・registry・chainを使用し、実頂点宣言、頂点／index、観測GPU定数、texture入力とsamplerを再供給しました。Built-in D3D11の限定領域で **各128/128、合計256/256画素RGBA一致、無効sample 0**。自然Viewer入力から全定数を再生成した比較、元の7段を通した全画面比較、原作とViewerの同一frame比較ではありません。候補shaderや実入力は公開しません。

CPUとの結合は独立に判定します。Addは対応なし、inverse VignetteAddは同じUnity frameの先行CPU呼出しが一意ですが、mode／inverseが一致する有効layerは複数あり、特定layerへの帰属は未成立です。global現在値の取得も上流writerの証明とは区別します。今回のdrawではUV ownerとの同一更新joinがなく、保存されたprojection／Film fullscreen UVから自然mask TextureMapping、中央下地camera、音声epochへの対応を推測しません。

採取側の宣言読取りを実PS・queue一致後の有効scopeへ限定し、元の固定上限内でreadbackを完了しました。私有WARP閉鎖試験19件と最終2 runのStopCompleted・保持handle復旧・通常終了後の配備復元を確認しました。ARM前Pending 4件、対象0件、readback未完了だったrunは各記録を保持します。今回の公開変更は文書のみで、既存の公開人工試験・Control結合・142秒自然再生を再実行した件数には加算しません。全false／Unknown gateを維持します。

## catalogを分離したprogram registry

複数の候補表から選択する場合は、`OriginalPostProcessProgramRegistry.Registration(catalog, candidateId, program)` を明示します。ResolveはDecisionの出所catalogを確認し、別のpass／catalogの同名IDへ自動接続しません。既存の直接ID解決経路も保持します。[PROGRAM_SELECTION.md](PROGRAM_SELECTION.md) に所有権と境界を記録しました。

最終ソースの公開選択試験は **125/125 PASS、4,096画素RGBA一致**。私有source退避後の公開Control結合は **376/376 PASS**。人工入力だけの後者は、別project Editorと共有DB・固定port・RT・出力を分離できる条件を確認して実行し、タイミングや性能の証拠にはしません。原作採取と自然再生は順次実行です。

私有検証では実データの通常Add／inverse VignetteAddから保存PS候補2件を追加し、実Controlの24評価で12,288画素、自然camera入力の32回のstandalone段で16,384画素が独立式のGPU参照とRGBA一致しました。これらはasset候補の診断で、この時点では追加2件の原作runtime選択は未証明でした。自然probeの入力はViewer camera color／raw depthであり、元の7段出力を経た完全な後処理入力とは区別します。

新規の限定原作runでは、早期準備に欠けていたparameter descriptorの作成順を修正し、同一更新・cameraのCPU値を採取できました。型metadata前の読取り拒否、prefix確認、hook creation closure、上限は維持します。評価済みFilm／Bloom→49成分のbit一致→選択PS→独立／連続中間の各896画素、最終128画素のRGBA一致まで同じrunで照合しました。子struct schemaの一部は同じ固定binaryの保存metadataを使い、実値を別runから補っていません。source clock・全画面RGBの契約や過去Pendingは変更しません。

## 現在のFilm・keywordを使うprogram provider

明示catalogと採点profileを受ける `OriginalPostProcessProgramSelection`、同一選択更新のFilmと現在keywordを結ぶ `OriginalLivePostFilmProgramProvider`、stage直前に登録programを解決するchainのPass overloadを追加しました。pass固有のkeyword範囲を適用した後にcacheし、同点は明示候補順を維持します。原作の実定数・native layout・候補shaderは公開実装へ含めません。

登録外program、未知family、古い更新、変更済みkeywordを別の候補や前回値で補完しません。無効Filmは描画なしとして返します。通常ライブへの自動登録と既存固定Passは保持します。source passや候補の適格性、保持globalの生成ownerは呼出し側が資格化する必要があります。API境界と実行方法は [PROGRAM_SELECTION.md](PROGRAM_SELECTION.md) を参照してください。

最終ソースの公開人工試験は **99/99 PASS、4,096画素RGBA一致**。私有source退避後の公開Control結合 **376/376**、route監査 **137/137**、既存chain試験 **46/46 PASS** も確認しました。私有の新規原作runでは7段のreadbackを完了し、同一runのkeyword・実pass・選択PSと保存入力を使った独立／連続中間の各896画素、最終領域128画素がRGBA一致しました。producer laneは今回観測したGPU定数であり、同runで取得できなかったCPU入力を過去runから補いません。

music1001の142秒自然再生では現在Film・keywordから88入力のprogramを選択し、616段を実行しました。対象pass外2入力を保留し、内部GPU replayは2,386,944画素RGBA一致。単一の確認済みprogramを登録した限定経路であり、原作全候補の自然選択や同一frame全画面一致ではありません。旧未完了readback・Pendingと各false／Unknown gateは保持します。

## selectorの実呼出しとcache entryの対応

追加の限定観測では、混合programのselector内で、検索不成立、近似検索の入力と戻り値、挿入側の再検索、呼出し後のentryを対応付けました。その後、同一runの対象drawで検索成功と同じprogramの返却を観測し、同一更新のPS bindingと全bytecode一致した生成objectへ結びました。既存の `CompareProgramKeywords` は集合比較のままで、Unityの選択処理を代行する実装には変更していません。

判定には完了した呼出し範囲、実際の検索結果、同じnamespaceとkey、選択program、描画側の一意なqueue範囲が必要です。検索不成立だけでentry追加とはせず、先行するentry追加と後のhitから連続lifetimeや全writer不変を推測しません。store命令の直接PC trace、追加時のcamera帰属、別familyや全候補の動作は別の未確定事項です。

今回の補助readbackは未完了です。描画前に取得したPS bindingと、画像を読出してRGBを比較する条件は別に判定し、readback未完了をRGB成功へ変更していません。従来の原作RGB比較・142秒自然previewは過去の独立した証拠です。公開文書のみの更新なのでUnity／公開Control試験は再実行せず、文書差分と公開範囲を確認します。

私有閉鎖検証はnative **18/18**、読取・判定model **8/8 PASS**。最終原作runのStopCompleted・保持handle復旧・通常終了後の所有配備復元を確認しました。途中のPendingと未起動の準備失敗は保持し、各false／Unknown gateも変更しません。

## CommandBufferのkeyword順序と実pass引数

`OriginalPostFilmRouteAudit.CaptureGlobalTransition(evaluatedFilm, basePass, initialGlobalKeywords)` は、呼出し側が明示した開始global集合に、既存の `Render.Apply` が記録する設定／解除だけを適用します。入力を変更せず、Film描画なしでは集合を保持します。開始集合が未取得の場合に空集合で補完せず、未実行CommandBufferの状態を `Material.shaderKeywords` や現在のglobal getterから推測しないでください。

MODE／blendの変更はCommandBufferのglobal命令です。material側のlocal keyword、他の保持global、先行する未実行命令は別の状態です。このAPIはUnityの最終variantを解決せず、原作の観測値としても扱いません。既存のslot zero保持はそのままです。

人工監査は **97/97 PASS**。既存の `ScreenOverlayRender.Render.Blit` を実行するGPU試験では、1 buffer内の2 draw、inverseによるpass選択、保持globalの有無、独立したmaterialの `COLOR_ONLY` を組み合わせ、**64/64画素のRGBAが完全一致**しました。記録時と実行後のglobal状態、後の命令が先のdrawへ遡及しないことも確認しました。Built-in D3D11の人工shaderによる試験です。公開Control結合も **376/376 PASS**。実行方法は下記のroute監査と同じです。

新規の限定原作観測では、対象7 drawそれぞれを同一run・更新・一意なqueue範囲のnative描画引数へ対応付けました。混合段の候補passが実引数とも一致する一方、最後の標準書戻しは全pass指定であり、literalなpass zeroと同一扱いしません。別の限定観測では、混合drawの実passと18 keywordの名前・global対応表・material側bitを同じconsumerへ結びました。描画前後で比較したのはspace／mask headerと参照global wordであり、全writer閉包の証明ではありません。

実keyword入力の有効集合と保存assetの宣言keyword集合は完全一致しませんでした。候補表の一意性だけで実variantへ昇格させず、keyword未指定時の選択と実program消費を別に確認します。今回の単一更新を、全楽曲・全918評価・自然keyword writerの成立へ拡張しません。

最終runでは混合drawのPSを限定し、全bytecode一致による生成object、保持reference、生成時刻がdrawより前であること、GPUの実PS objectを照合しました。同じdrawの実passにより、保存asset内のPS aliasも一意になりました。実keyword入力と宣言keyword集合の差はそのまま残ります。内部のfallback選択分岐そのもの、最後のcopy shader identity、全variantのproducer契約は未確定です。

## programのkeyword集合を分ける監査

`OriginalPostFilmRouteAudit.CompareProgramKeywords(effective, lookupKey, selectedProgramKey, declaration)` は、明示された4集合の隣接する差と一致判定を返すEditor専用APIです。runtime検索キーと選択候補のキーを別々に扱うため、選択候補の宣言にだけ存在するkeywordを、実際の有効bitへ昇格させません。program identity、同一更新、cacheのhit／miss、選択分岐の証明は別途必要です。

前回の限定原作観測では、同じ混合drawの検索cache、選択programとholder、GPUで消費したPS、全bytecode一致した生成objectを対応付けました。検索側にないkeywordを含む候補が選ばれており、資格化したmiss時の近似検索と同点先頭優先の評価結果にも一致します。これはkeyword setterがそのbitを有効にした証明ではありません。その前回drawのcache hit／miss分岐と過去のcache生成呼出しは未traceのままです。

空集合を渡す場合も呼出し側の明示入力が必要です。欠落入力、重複、不正token、上限超過は拒否し、取得した集合を複製して保持します。原作の内部layout、保存値、候補bytecode、選択式を公開実装へ含めず、通常rendererと従来のroute監査を維持しています。検証は既存の `OriginalPostFilmRouteAuditValidation.Run` で実行します。

最終ソースで **137/137 PASS**、D3D11人工描画の **64/64画素RGBA一致**、公開Control結合 **376/376 PASS** を確認しました。原作RGB replayや142秒の自然previewの再実行ではありません。追加の原作採取は、正常停止と観測成立を別判定で保存し、途中のPendingや不成立を最終runの成功へ置き換えていません。全false／Unknown gateを維持します。

## 評価済みFilmのcode route監査

Editor専用の `OriginalPostFilmRouteAudit.Capture(evaluatedFilm, basePass)` は、評価済み `PostFilmUpdateInfo` と呼出し側が資格化したbase passを受け取ります。現在の `ScreenOverlayRender.Parameter.IsValidity` とkeyword配列を読み、有効なFilmのpass加算とMODE設定／解除、通常color layerで解除されるblend keywordを返します。keyword配列のslot zeroが保持される動作も変更しません。無効FilmはFilm描画なしとして返し、別のdefault／copy経路を補完しません。

これは現行Viewerコードの説明で、原作の実行pass・variant選択やcapability ownerの認定ではありません。`COLOR_ONLY`、stereo等の保持状態を生成せず、color typeをそのwriterと解釈しません。material availabilityと外側の描画分岐も別入力です。未資格化のmovie layer、未知mode／color layout、非有限な有効判定値、不正base passを拒否します。描画やglobal keywordの変更、通常ライブへの登録は行いません。

人工の `OriginalPostFilmRouteAuditValidation.Run` は **56/56 PASS**。modeごとの有効判定・MODE名、inverse、ゼロpower、monochrome alpha、保持slot、拒否条件を確認しました。私有source退避後の公開Control結合も **376/376 PASS**。専用Editorで `-executeMethod OriginalPostFilmRouteAuditValidation.Run -integrationOutput <外部出力先>` を指定し、`route-validation.json` を取得できます。

私有監査ではmusic1001の演出360 keyの一覧と、実Controlを使う918サンプルの表を作成しました。Film252 payloadがserialized field単位でroundtripし、918/918で評価値を取得、332サンプルは補間を使用しました。明示audit camera・保存データ・選択frameによるreplayで、原作owner／音声epochや自然再生との同期は未成立です。

保存資産の276個のprogram aliasを検査し、Film描画なし130、両effect有効で候補variantが各1つとなる772、混合familyの条件外16へ分けました。772は必要な保持keywordが実際に有効である証明ではなく、通常rendererへ自動採用しません。別途、保存原作の先行5 drawは同一runの生成program→object→消費を再照合し、資産内pass候補を各1件へ絞りました。実pass引数の直接観測、混合／copyのprogram identity、別run間のframe対応はUnknownです。旧cache上限と過去Pendingも保持します。

追加採取の準備案は閉鎖GPU fixtureで拒否され、配備・controller・ゲーム起動・ARMを行っていません。新規原作runとRGB／自然再生の追加成功数は0です。次の採取はPS単体でなく、materialのpass指定・keyword状態と同じdrawを結ぶ観測境界が資格化された場合に限定します。

## typedライブデータの読取監査

今後の調査は、選択楽曲の設定とtyped timeline、関連Prefab／Material／Shader、既存CPU／GPU観測の順に照合します。データにある演出指定、素材上の参照・pass・宣言keyword、実行時の選択・GPU消費は別の証拠です。データや名前の一致だけでruntime bindingを認定しません。追加の原作runは、データと既存コードで解決できなかった有限の観測対象に限定します。

Editor専用の `OriginalLiveDataInventory.Capture(loadedData, includeKeyPayload: false, maxKeys: 100000)` は、既存loaderが返した `LiveTimelineData` を受け取ります。worksheetのserialized fieldを辿り、`ILiveTimelineKeyDataList` を元順序で列挙します。key index・frame・attribute・型、nullと空、worksheet対象cameraと有効flag、groupに明示されたmulti-camera番号を保持します。camera番号はauthored値であり、原作receiver／cameraへのruntime帰属を自動的に確定しません。raw fileの解析や新たなloader、key選択・補間・描画への登録は含みません。

既定ではkey payloadを取りません。`includeKeyPayload: true` はtyped keyのserialized fieldをJSON文字列として保持するため、実データの出力はprivateな保存先へ限定してください。importされていないfieldや未知componentは復元せず、原作の全raw keyを取得できたとは扱いません。worksheet64、track4096、group4096、深さ16、key最大100000、payload一件65536文字・合計8Mi文字の上限を持ち、超過時は例外で停止します。

人工の `OriginalLiveDataInventoryValidation.Run` は **22/22 PASS**。空・null・元順序、同じlistの異なる参照path、通常／multi-cameraの明示値、payload opt-in、読取前後のkey選択状態、key／worksheet／payload上限を確認しました。私有source退避後の公開Control結合も **376/376 PASS**。既存のUnity wrapperと同じ専用Editor環境で、`-executeMethod OriginalLiveDataInventoryValidation.Run -integrationOutput <外部出力先>` を指定できます。結果は `inventory-validation.json` です。

今回のローカル監査はmusic1001の78 bundle、typed keyリスト225本・5232 key、Prefab 9件、Material 22件、object参照91件を取得しました。Bloom／Diffusion108 key、Film252 key、位置2groupとFilm2groupのmulti-camera番号を保持しています。実値・参照識別子・資産・採取scriptはrepoへ含めません。DB／対象bundleの前後hashは不変です。

保存GPU観測との対応は、先行6 drawのshader名と関連2 shader資産、assetのpass一覧・宣言keyword、Dimmer既定値の限定一致までです。原作の同一drawで選択されたpass／variant・samplerや最後のshader生成API identityはUnknownです。3件のnull component slotは所在を保存しましたが、Missing MonoBehaviour判定は0件で、型や実行機能を推測しません。追加原作run・自然再生・RGB試験は今回実施せず、既存の成功記録と全false／Unknown gate・過去Pendingを保持します。

## 現在materialとglobalの対取得

`OriginalPostProcessPropertyState.Capture(frame, input, material, materialProperty, globalProperty)` は、明示materialのColor／Vectorと別のglobalを同期取得します。`MaterialVector` と `GlobalVector` はraw値で、色空間変換や時間からの推測を行いません。欠落material、宣言されていないmaterial property、型不一致、非有限値、material側に宣言されたpropertyをglobalとして指定する呼出しは拒否します。

同期prepare内で `ValidateCurrent(frame, input)` を実行してください。camera、selected／render serial、frame color／depth参照、入力と寸法、material lifetime／shader、現在の両property値を再照合します。RT内容の凍結や外部変更のrollbackは行いません。materialは呼出し側が所有します。

`Shader.GetGlobalVector` はglobalの存在、writer、更新ownerを証明しません。未設定時のUnity getter結果を採用できるか、当該drawがmaterial overrideやMaterialPropertyBlockよりglobalを消費するかは、呼出し側が別に資格化する必要があります。このAPIは未宣言のmaterial overrideを解決しません。保存値・ゼロ・白を未確認producerの代用にしないでください。

`-PostProcessDrawOnly` の公開人工GPU試験はBuilt-in／URP14で **416/416 PASS、2240画素RGBA一致**。異なる2更新でglobalのUV移動とmaterial色変化を描画し、微小変更、更新不一致、別入力、materialのshader変更・解放、型不一致、非有限4成分を拒否しました。私有source退避後の公開Control結合も **376/376 PASS**。既存DrawState API・shader・未登録経路は変更していません。

私有の限定原作観測では、実UV writerのowner fieldからglobal setter・現在global・同じmixed drawへ接続し、Dimmerも名前で特定した現在material／shader既定stateから同じGPU定数へ結びました。消費6成分、既存draw248成分、producer49成分がbit一致し、独立896・連続中間896・最終128画素RGBA一致を確認しました。全writer lifetime、非ゼロUVや可変Dimmer遷移、shader生成API identityの証明ではありません。

私有候補7段では保存effect vectorをcontextから除去し、現在のViewer global getterと、ロード済みeffect shaderから生成したmaterialの現在propertyを毎入力で取得します。142秒自然再生は7939描画、左右同時58更新、対象114/116入力、798段、内部replay2727936画素RGBA一致、終端・Viewer復帰・fault／例外0でした。2入力は候補Film条件外で保留、観測UVはゼロ、Viewer内global writer／presenceは未確定です。87参照はproducer／draw85＋現在property観測2という供給源分類であり、原作と同じ自然producerが全て接続できたという判定ではありません。全false／Unknown gateと過去Pendingを維持します。

## 現在materialのvector取得

`OriginalPostProcessDrawState.Capture(frame, input, model, view, projection, inputYFlipped, uvScale, uvOffset, vectorSource, vectorProperty)` は、指定materialに宣言されたColor／Vector propertyの現在値を `MaterialVector` に保持します。shader既定値も、現在のmaterialから取得します。property欠落、Float／Texture等の型、非有限値は拒否し、白色やglobal値で補完しません。従来overloadの `MaterialVector` はnullです。

値はraw vectorで、色空間変換、MaterialPropertyBlock、global overrideの解決は含みません。materialは呼出し側が所有します。同期prepareで `ValidateCurrent(frame, input)` を実行し、既存のcamera／serial／texture検証とともに、material解放、shader変更、property値の変更を検出してください。変更後に描画する場合は、そのdraw用に再取得します。RT内容の凍結や、外部状態のrollbackを行うAPIではありません。

`-PostProcessDrawOnly` の公開人工GPU試験はBuilt-in／URP14で **314/314 PASS、2112画素RGBA一致**。従来UV試験に加え、materialの既定値と更新した非白色・alpha・ゼロ値を実際のGPU入力へ接続しました。微小なproperty変更、shader変更、解放、欠落、型不一致、非有限4成分の拒否も確認しました。私有source退避後の既存Control結合は376/376 PASSです。

私有候補7段の書戻しでは、現在のコピー用material propertyを毎入力で取得し、保存tintをcontextから除去しました。限定原作runの同一drawでは、shader既定state→現在state→実PS定数の色4成分がbit一致しています。そのrunの保存入力replayはdraw248成分・producer49成分bit一致、独立896・連続中間896・最終128画素RGBA一致、invalid sample 0でした。生成API identityや全variantの一致は未証明です。

最終ソースの142秒自然再生は7437描画、左右同時53更新、対象106入力すべてで現在tint供給、保存tint使用0、742段、内部replay2727936画素RGBA一致、終端・Viewer復帰・fault／例外0でした。原作との自然な非白色tint遷移や同一frame RGB一致へ拡張しません。静的register参照の分類は現在85／retained 2で、残るUV scroll・Dimmerと、全false／Unknown gate・過去Pendingを維持します。

## 現在drawの書戻しUV引数

`OriginalPostProcessDrawState.Capture(frame, input, model, view, projection, inputYFlipped, uvScale, uvOffset)` は、当該draw commandの明示UV引数を `UvScaleOffset=(scale.x, scale.y, offset.x, offset.y)` として保持します。非有限値を拒否し、既存の `ValidateCurrent` でcamera、render／selected serial、textureと寸法の一致を検証します。引数を持たない従来overloadは単位scale・ゼロoffsetを使用し、既存の行列とtexel値は変更しません。利用側は同期prepare内で、そのdrawが実際に使う引数を与えてください。

この値はtexture寸法から推測したTextureMappingではなく、明示したdraw引数です。tint、UV scroll、Dimmer、sampler、backend固有の投影生成元は補完しません。serialは入力RTの内容を凍結せず、同一frame内の別drawの引数まで自動判別しません。

`Invoke-IntegrationValidation.ps1 -PostProcessDrawOnly` の最終人工試験はBuilt-in／URP14で **234/234 PASS、1920画素RGBA一致**です。単位変換、横反転、部分切出し、明示Y反転と異なる寸法を実際のshader入力へ渡し、全RGBAを照合しました。stale／foreign／released入力と非有限UVも拒否します。

私有の候補7段では書戻し直前に現在draw stateを作り、そのUVを接続します。保存UVをcontextから除いても、保存原作入力のdraw244成分がbit一致し、独立896・連続中間896・最終128画素のRGBA一致を維持しました。今回追加した4成分以外の既存draw240成分、CPU producer49成分は同じ保存観測の再評価です。

最終ソースの142秒自然再生は7910描画、左右同時60更新、終端・Viewer復帰・fault／例外0でした。120 camera入力のうち118入力でUVを現在drawから再生成し、保存UV使用0・826段・内部replay2727936画素RGBA一致を確認しました。2入力は候補Film条件外として保留です。原作同一frameの自然RGB一致や、全状態での書戻しshader identityは未証明です。残るretained 3参照と全false／Unknown gate、過去Pendingを維持します。

私有source撤去後の既存Control結合も **376/376 PASS**。

## MipBiasの現在入力供給

`OriginalPostProcessSamplingMath.EvaluateMipBias(pixelWidth, targetWidth, dynamicWidthScale, temporalMipBias)` は明示した4入力からbias／gainを生成します。target幅とdynamic scaleのfloat32積、pixel幅とのfloat32除算、doubleの対数・符号反転、float32への変換、上限ゼロとtemporal値のminという境界を保持します。非有限値、非正の寸法・scale、積のoverflowや比のunderflowは拒否します。

`OriginalCameraFrame.PixelWidth` はprivate render cameraにtargetが束縛されている間に取得します。`DynamicWidthScale` は同じcameraのdynamic-resolution設定に応じた値です。`TemporalMipBias` はそのrender cameraのURP設定が存在する場合だけ値を保持し、未供給をゼロに読み替えません。`StereoEnabled` も同じcameraの観測です。source cameraの追加componentを複製するAPIではありません。

`frame.Color.width` は要求した出力幅であり、任意のURP内部descriptor幅の代用ではありません。利用側はrender scale、XR、target／descriptor対応を資格化してください。今回の私有自然接続では非stereo・URP render scale 1の範囲で現在temporal値を使用し、欠落・対象外backendを保留します。Built-inで同じ算術を使う場合も、temporal依存は呼出し側の明示policyが必要です。Bloom等の縮小passの幅をcamera用の幅へ代入しません。

`Invoke-IntegrationValidation.ps1 -PostProcessSamplingOnly` は人工試験です。通常／縮小／dynamic幅、temporalの優先、非2冪の比でのfloat丸め、異なるmip色のGPU選択、現在のprivate camera由来の値とsource camera設定との分離を確認し、Built-in／URP14合計 **53/53 PASS、256画素RGBA一致**でした。他の専用modeや観測profile指定との混在はEditor起動前に拒否します。

原作側の対応methodは今回の正常終了runでmetadata／実prologへ結び、限定した命令と数値依存を確認しました。既存原作保存入力に対して、等倍・temporalゼロの明示contextで再生成した24成分がGPU保存値とbit一致しました。これは同一更新のCPU cameraDataを保存した検証ではありません。7 drawの独立896画素、連続中間896画素、最終128画素のRGBA一致は同じ保存観測の再評価です。観測数へ合算しません。

最終自然再生は142秒、7905/7905描画、左右同時59/59、Viewer復帰・fault／例外0でした。118入力全てでrender cameraの現在URP temporal値を観測し、前回値やBuiltin用policyを代用せず826段へsamplingを供給しました。内部replayは8回・2727936画素RGBA一致です。実測biasは0であり、自然な非ゼロbiasや原作同一frameのmip/RGB一致まで示す結果ではありません。最初の全保留runは別に保持し、私有wrapperは7段の実処理数・sampling供給数・backend保留0・内部replay一致も要求します。再生が完走しただけでは後処理の成功になりません。

MipBiasに対応する6参照をproducerへ移しても、UV scroll、Dimmer、書戻しUV／tint、projection生成元、variant／samplerまで確定したことにはなりません。原作同一frameの自然RGB一致は未証明で、全false／Unknown gateと過去Pendingを維持します。

## Fullscreen draw状態の生成

`OriginalPostProcessDrawState.Capture(frame, input, model, view, projection, inputYFlipped)` は、同期frame callback内で使用します。fullscreen用の明示model／view／projectionから `ObjectToWorld` と現在backendの `GpuViewProjection` を生成し、そのdrawが読むtextureの現在寸法から `TexelSize` を生成します。Y符号は呼出し側が明示します。`frame.Projection` はcamera映像の生成に使った行列であり、fullscreen用として自動採用しません。

利用側はchainの `prepare` 内で現在の先行段textureを選び、Captureと `ValidateCurrent(frame, input)` の後に、確認したshader layoutへ値を設定してください。色・depth・Film等のproducer値とは独立したdraw情報です。model、view、projection、入力texture、Y符号に省略時の推測値はありません。古いrender／selected serial、別camera、別color／depth参照、別texture、解放・サイズ変更を検出した入力、非有限行列は拒否します。serialによってRT内容の不変性や寿命が延びるわけではありません。

汎用APIに原作の定数buffer layout・投影範囲・mesh・MipBias・UV scroll・Dimmer・書戻し変換・variantは埋め込みません。これらの意味と供給元は利用側の責任です。特定の保存CBに数値一致することから、その値を作った原作API／writerの一意性や未観測状態まで確定しません。

`Invoke-IntegrationValidation.ps1 -PostProcessDrawOnly` は人工transformと非対称textureによるBuilt-in／URP14試験です。異なる入力寸法、明示Y反転、全RGBA、stale／foreign／released入力を確認し、**78/78 PASS、640画素一致**でした。観測profile指定や別の専用modeとの混在はEditor起動前に拒否します。既存Control結合376/376も公開ソースだけで再検証しています。

私有の候補7段接続では、opaqueな保存CBの読込みを外して、必要な値だけを生成・明示設定する方式へ切り替えました。対象programの静的参照全てに供給元を割り当て、参照されない保存CBの値はGPUへ渡しません。保存入力のdraw情報240成分のbit一致、CPU producer49成分の一致、7 drawの896画素、連続した各中間段896画素と最終128画素のRGBA一致を確認しました。比較領域の範囲外sample／無効入力伝播は0です。Built-in D3D11の同じ保存観測に対する再評価であり、新規の原作run／独立観測ではありません。

同じ経路で142秒の自然再生が終了・Viewer復帰し、7980/7980描画、左右同時59/59、116入力・812段のdraw状態再生成、8回・2727936画素の内部RGBA replay一致を確認しました。候補Film mode／color条件を満たさない2入力は処理成立に含めません。内部replay一致は原作同一frameとのRGB比較ではありません。投影範囲・Y規約・mesh・効果global・variant／samplerには明示候補contextの依存が残り、全自然生成を認定しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 描画ごとの準備と中間出力通知

`OriginalPostProcessChain.Binding(property, Func<Texture>)` は、外部textureを各drawで1回解決します。登録時にdepthがまだ存在しなくても、frame callback内で現在のdepthを返せます。null・解放済みRT・現在の出力へのaliasは描画前に拒否し、前回値へfallbackしません。従来の固定textureと前段参照も使えます。

`Process(camera, input, prepare, completed)` の `prepare(stage, material)` は各段のtexture bindingより前、`completed(stage, target)` は描画直後に同期実行します。定数変換の意味は呼出し側が明示します。callback内でcopy・readbackしないRT参照は次回再利用時に変化します。例外、再入、処理中のdisposeでは成功件数を進めず、active RTとsRGB writeを復元します。callbackが変更した呼出し側所有materialの値はrollbackしません。旧2引数のProcessはcallbackなしで同じ経路を使います。

公開試験は7段を2更新描き、現在のdepth・typed payload・段順序・各段のUNorm量子化後のRGBを独立したCPU式で確認します。欠落depth、自己alias、prepare／completedの例外、再入とdispose、状態復元も検証します。追加経路を含む自然入力試験は **105/105 PASS、4352画素**、後処理試験は **46/46 PASS**、Control結合は **376/376 PASS**。いずれもBuilt-in／URP14を含みます。

私有の保存原作入力でも全段をprepareで設定し、現在depthを明示resolverへ渡しました。独立drawの896画素、連続生成した各中間段の896画素、最終128画素がRGBA一致しました。後二者は同じ保存参照を再評価した結果で、新しい原作観測ではありません。この段階ではPostFilm関連の自然更新は未供給でした。その後の自然key接続は末尾の「PostFilmの自然key供給」を参照してください。

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

CPU算術追加の時点では142秒の自然楽曲を再実行しておらず、PostFilmの連続timeline providerも未接続でした。次節はその後の追加成果です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## PostFilmの自然key供給

`bridge.UseConfirmedPostFilm(confirmedFilmProfile)` が、既存Controlの選択・補間helperを使う追加経路を有効にします。profileには確認済みの時間・角度演算の依存値、roll／scaleの保持成分、depthの明示状態を渡します。原作値の既定profileは公開repoへ埋め込みません。MovieとBlinkLight同期には別のproviderが必要です。

`Frame.Films` は通常camera／multi-camera、3層、key選択、実際に使った補間率、評価済みpayloadを更新ごとに保持します。multi-camera用keyの番号は、同じ番号のposition trackを一意に探して既存Viewer cameraへ結びます。main cameraへのfallbackはありません。新しいBeginFrameで前回のFilmを破棄します。未接続分岐は値を持たない観測として残し、未通知や曖昧な結合から既定値を作りません。

```csharp
provider.Bridge.UseConfirmedPostFilm(confirmedFilmProfile);
var film = new OriginalLivePostFilmProvider(
    () => preview.RenderingFrame, confirmedFilmProfile, 1);
var parameters = new OriginalLivePostProcessParameters(
    () => preview.RenderingFrame, confirmedPostProcessProfile,
    (frame, values) => ProcessQualifiedPrograms(frame, values),
    film.Resolve);
preview.SetInputFramePostProcessor(parameters, confirmedDepthCopyShader);
```

上のprogram、binding、profileは呼出し側が明示します。providerは同じserial・camera・layerの一意なFilmだけを変換し、欠落はnull、未接続分岐は拒否します。Filmを必要とするprogramがnullを受け入れるかは、既存のcallback契約と同様に呼出し側が判断します。従来の明示Film providerと未登録時の描画は保持します。

`Invoke-IntegrationValidation.ps1 -PostFilmOnly` は、人工keyの4色配置、scalarのfloat32演算、保持値、未接続分岐、実Controlの選択と補間、camera／layer／serialの境界とBuilt-in／URP14のGPU接続を検証します。**33/33 PASS**。既存Control **376/376** と既存producer **44/44** も個別に通過しました。専用modeや実測入力引数との併用は起動前に拒否します。

原作の限定観測では同一呼出しのkeyと評価直後payloadを保存し、補間しない13件でfloat32 390成分がbit一致、その他104成分も一致しました。curve入力が不足する3件はこの直接比較に含めていません。補間分岐の静的命令確認と人工検証を、自然curve遷移の実測へ拡張しません。

最終runtimeの自然再生は142秒を完走し、7980更新を描画、左右同時有効59/59更新、Film観測24061件を確認しました。118 camera入力のうち116入力で私有の候補7段を実行し、812段が完了しています。8入力の各段を同じ現在入力から別GPU描画で再合成し、2727936画素の全RGBAが一致しました。8入力とも処理前後のRGB変化を確認しました。2入力は候補経路の成立件数へ含めません。

この私有試験の自然入力は現在のViewer color／raw depth／typed payload／Filmです。保存RGBや保存Film keyは供給しません。ただしprogramとfullscreen用global templateは保存済み候補を明示し、特定のmode・色配置・入力条件に限定しています。その他のglobal生成、variant選択、Movie／BlinkLight、自然TextureMapping、原作時刻原点、同一原作frameのRGB、最後のshader生成API identityは未確定です。原作に対する画素一致試験とは区別します。

私有probeの初回は未使用texture slotの取得で停止しました。明示graphから同じ更新のRTを選ぶよう修正し、短い2更新・14段・2368画素のGPU preflightを通してから142秒を再実行しました。公開rendererを変更した回避ではありません。失敗記録は成功runとは別に保持しています。

原作runは2件でStopCompletedと配備復元を確認し、先行のmetadata上限停止runはPendingと通常終了後の配備復元を別々に保持します。公開物に採取値、native source、shader program、画像、receiptを含めません。各Unknown flagと過去Pendingは変更しません。

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
