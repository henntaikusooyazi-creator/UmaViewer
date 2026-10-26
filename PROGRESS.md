# 原作方式ライブレンダラの進捗

更新日：2026-10-27。通常ライブへ自動採用しない、明示登録の **TestOnly** 開発ブランチです。

> この文書は独立検証ブランチ `codex/validate-original-live-integration` の状態です。公開済み `feature/original-live-rendering` は `781ef89`（7コミット）のまま保持しています。

## 楽曲時刻writerと時計getterの限定引継ぎ

原作の実Updateから、楽曲用playbackの整数戻り値をfloat32へ変換し、秒へ換算してcached時刻へ直接書く経路を限定しました。隣接するBGM用playbackとは区別します。通常setterを観測しても呼出しがなかったため、同じ採取を繰り返さず、実metadataと保存命令から直接書込みへ追跡しました。

最終の限定runでは、開始直後と後半の保存16件で同じplayback識別子・cue sheetを確認しました。cue sheetは既存Viewer loaderの伴奏候補と対応し、対象資産のhashも不変です。これは波形内容やPCM epochの一致証明ではありません。

時計getterが先に、楽曲時刻の取得・書込みが後に実行されていました。保存された連続getter間にある実戻り値について、receiver・thread・owner・実frame・呼出し順・前後field値を照合し、**12遷移すべてで次のgetterのfloat32 bitと一致**しました。同一frameの先行getterへ直接結合できた件数は0のまま保持します。全更新へ固定の1 frame補正を加える契約にはしません。[時計の記録](Tools/OriginalLiveRendering/AUDIO_CLOCK_CHECKPOINT.md) に範囲と再発防止を記載しました。

私有の最終native専用試験は30件、Pythonの読取・正確な丸め・引継ぎ判定は72件PASSです。後者の2 moduleはstatement／branchとも100%で、collector全体や解析CLIのcoverageではありません。採取runの外側Pendingとmetadata slot不変条件は保持し、hook解除・drain後の通常終了と所有配備復元を別に確認しています。

今回の公開変更は文書のみです。通常時計やrendererは変更せず、既存498件・142秒Viewer再生・原作RGB replayも再実行していません。原作対ViewerのPCM epoch、GPU command生成owner、自然projection／mask、中央Dの残るwriter、同一原作frameの最終RGBは未確定です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown`を維持します。

## CPU Filmとowner値からの限定RGB再生成

通常Addでは、新しい同一原作更新のCPU parameterと安定したowner値を結び、実Controlで補間を再評価しました。Filmの消費 **24/24成分がfloat32 bit一致、128/128画素RGBA一致**。inverse VignetteAddは過去の独立runを再評価し、消費 **17/17成分** に一致するlayerの値の対応が一意となり、そのCPU Filmから **128/128画素RGBA一致** を確認しました。

Film以外のglobal、texture、geometry等は実drawの観測入力を保持しています。未使用laneの差も保存し、全CB一致、camera帰属、全入力の自然生成とは扱いません。owner値からControlへの一点の対応と、音声epochを含む連続同期も別です。

追加の実機採取では、原作内の同じ更新に属するplayback識別子、時刻controller、Director、Control owner値を限定的に対応付けました。現在値のfloat32 bit一致とowner整数値への変換を確認しています。音声APIの実戻り値、Viewer側epochとの対応、seek／pause後のlifetimeは未成立です。

ライブデータから左右maskが実際に有効な区間を選び直し、左maskの同じdrawから頂点・index・UV・viewportを保存しました。Film用の採取区間では左右maskが無効だったため、用途ごとの区間確認を採取前の条件にしました。このmask runの時刻記録は安定性条件を満たさず除外し、別runの時刻成功と結合していません。投影設定メソッドで確認した行列コピーをconstructorとは扱いません。中央下地camera、自然projectionと補間生成、同一原作frameの全画面RGBAは引き続き未解決です。[対応記録](Tools/OriginalLiveRendering/NATURAL_INPUT_JOIN.md) に証拠の範囲と次の受入条件を記載しています。

公開変更は文書のみです。正常停止runと途中の上限拒否・接続切断Pending、ゲーム未起動の準備不成立を分離し、所有配備の復元を記録しました。今回の私有試験を、既存の公開Control試験や142秒自然再生の再実行件数へ加算しません。各false／Unknown gateを維持します。

## 追加2候補の原作実行選択と限定RGB比較

通常Addとinverse VignetteAddのそれぞれを、新しい別々の原作runで確認しました。実pass引数、現在keyword、runtime候補表の選択結果、program／holder、生成時の全bytecodeが一致するPS object、GPUが消費したPSを、同じrunの一意な描画queue範囲へ結びました。保存資産との候補表照合は全行を対象とし、選択された候補だけの名前照合ではありません。

| 対象 | 原作の実pass | 有効keyword | 選択候補のkeyword | Viewerでの限定比較 |
| --- | --- | --- | --- | --- |
| 通常Add | 5 | `MODE_ADD` | `MODE_ADD`, `COLOR_ONLY` | 128/128画素RGBA一致 |
| inverse VignetteAdd | 6 | `MODE_VIGNETTE_ADD` | `MODE_VIGNETTE_ADD`, `COLOR_ONLY` | 128/128画素RGBA一致 |

どちらも `COLOR_ONLY` の有効化を仮定せず、現在の検索キーと候補の宣言keywordを分離したまま既存の選択器・registry・chainを使用しました。Built-in D3D11で、実drawの頂点宣言・頂点／index・GPU定数・texture入力・samplerを用いた各128画素、**合計256/256画素の全RGBA一致、無効sample 0**です。私有shader側のsampling設定は観測されたsamplerへ合わせ、出力補正や自動位置合わせは加えていません。これらは2つの独立したdrawの比較であり、同一原作frameの7段連続比較ではありません。

入力は同じdrawで観測したGPU値です。通常AddはCPU parameterとの対応が未成立、inverse VignetteAddは同じUnity frameの先行CPU呼出しと対応するものの、mode／inverse条件に合う有効layerが2つあり、固有layerの帰属までは認定しません。今回、CPU producerの全定数再生成、URPでの原作入力比較、142秒自然再生、公開Control試験は再実行していません。以前の成功件数は加算しません。

| 自然入力・同期の対象 | 今回の確認範囲 | 残る境界 |
| --- | --- | --- |
| global／material property | 対象drawの現在値とGPU入力を保存。既存UV setter呼出しを含む関数境界と4直接calleeを同じ原本で限定静的確認 | 上流writerの正式なowner／lifetimeと非ゼロ自然遷移は未確定。今回の2 drawではUV ownerとの同一更新joinは成立していない |
| projection・頂点／補間 | 対象drawの実入力で限定RGBを再現 | 自然projection constructorの帰属は未確定 |
| mask TextureMapping | 既存mask検証を保持 | Film fullscreen入力から自然maskの生成元へは昇格しない |
| 中央下地camera | 既存の左右に限ったwriter帰属を保持 | 今回のFilm drawは中央下地Dのwriter／camera証明ではない |
| 時刻・音声epoch | 原作draw内のframe対応を保存 | cue採取の待機条件、Unity frame、owner比較値を同一視しない。原作対Viewerの音声epoch・同一frame最終RGBは未成立 |

採取側は、選択対象外の描画で頂点宣言の読取り枠を消費する問題を修正し、実PSと一意なqueue範囲が一致した有効scope内だけで読み取ります。元の読取上限と停止条件は維持し、私有WARP閉鎖試験19件で対象外scopeと実drawのreadbackを確認しました。最終2 runは **StopCompleted・同一保持handleによる復旧・通常終了後の所有配備復元** が成立しています。途中4件のARM前Pendingと、RGB未完了だったrunは別記録のまま保持します。

公開変更は検証範囲の文書更新です。原作の採取値、native layout、候補shader、私有collectorは含めず、通常renderer・既存API・成功ブランチを変更しません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。次の入力資格化は、global ownerへの上流代入、projection生成呼出し、maskのwriterと中央下地のresource版、音声位置とownerの同一更新観測をそれぞれ別のedgeとして扱います。

## 複数programの登録と同一runのCPU→GPU照合

`OriginalPostProcessProgramRegistry` を追加しました。catalog instanceと候補IDを組にして登録するため、別passの同名IDや、同じpass／IDを持つ別catalogへ誤接続しません。選択結果は出所を保持し、登録なし・重複・変更済みmaterialを拒否します。通常rendererへの自動登録は追加していません。

公開人工選択試験は **125/125 PASS、4,096/4,096画素RGBA一致**。私有source退避後の公開Control結合も **376/376 PASS**。後者は人工typed入力と一時sceneを使用する条件を確認し、別projectのEditorとprocess・RT・出力を分離して実行しました。共有環境の性能測定には使用しません。

music1001の既存データから、追加対象として通常Addとinverse VignetteAddの2つの保存PS候補を限定しました。72 keyを実Controlへ渡した24評価（usedRatioあり8件）で、独立した式のGPU参照と **12,288/12,288画素RGBA一致**。この時点では保存資産の候補を使う比較で、2候補の原作runtime選択・生成identityは未確認でした。候補shader・実定数・実keyは公開repoへ含めません。

最終ソースの142秒自然再生は **7,352/7,352更新、左右同時50/50、自然終端・Viewer復帰**。既存7段は100入力・700段、内部replayは **2,727,936画素RGBA一致**。追加2候補は、同じ選択更新のFilmに属するViewer cameraのcolor／raw depthで各16回、合計32回のstandalone probeを行い、**16,384画素RGBA一致**しました。これは追加overlay段単体の自然入力診断であり、元の7段出力を入力する完全な後処理列や原作中央cameraの帰属を認定しません。provider／render fault・例外は0です。

原作CPU入力が前回0件だった原因は、早期shader監視のhook準備が型metadata取得より先に走り、条件付きのparameter hook作成が省略されていたことでした。最初のenable後のhook追加は禁止する既存方針を保持し、entry prefixを確認した無効descriptorを先に準備します。型metadata未確定時の入力読取り拒否と元の上限は維持しました。私有の追加閉鎖試験16件と既存ABI等214件を通し、新規原作runはStopCompleted・保持handle復旧・通常終了後の所有配備復元まで成立しています。

同runの対象camera・更新に一意に結んだ評価済みCPU Film／Bloomから、**49/49成分float32 bit一致**を再確認しました。子structのschemaには同じ固定binaryの保存metadataを使用し、親の現行metadataとstrideを再照合しています。CPU値は今回のrunだけを使います。同じrunの実pass・keyword・選択PS・GPU消費へ結び、CPU生成定数と登録programで **独立896/896、中間連続896/896、最終128/128画素RGBA一致**。同じ観測の再評価を独立観測数へ合算しません。

この段階で未確定だった2候補の原作実行選択は、上記の追加観測へ引き継いでいます。全候補の適格性lifetime、自然global writer、TextureMapping、中央camera、音声epoch、原作の同一frame全画面RGBは引き続き別の未確定事項です。過去の未完了readback・Pendingと、各false／Unknown gateを維持します。

## 自然Filmからのprogram選択とreadback後のRGB比較

確認済みの選択規則を、呼出し側が明示する候補表・適格性・採点profileとして接続しました。同じ選択更新のFilm、camera、現在global／material localを取得し、既存のFilm命令順を適用してからpass固有のkeyword範囲へ投影します。選択IDは明示registryからstage直前に解決します。通常UI・固定Pass・既存のkeyword writerは保持し、原作の定数や候補shaderは公開コードへ含めません。[APIと境界](Tools/OriginalLiveRendering/PROGRAM_SELECTION.md) を参照してください。

公開人工検証は **99/99 PASS、4,096/4,096画素RGBA一致**。実Controlの選択からBuilt-in／URP14の動的program描画までを独立した固定program経路と比較し、別FilmでRGBが変わること、cache、inverse、Filmなし、stale状態と描画直前のkeyword変更を確認しました。私有sourceを退避した公開Control結合 **376/376**、route監査 **137/137**、既存chain **46/46 PASS** も最終ソースで確認しています。

新しい限定原作runでは、描画追跡経路を保持して7段すべてのreadbackが完了しました。同一runの実pass、keyword、選択programと全bytecode一致したPS生成objectをGPU消費へ結び、選択規則を通した候補programで **独立896/896、中間連続896/896、最終領域128/128画素のRGBA一致**を確認しました。同じ保存観測の別再評価であり、独立観測数へ合算しません。このrunはCPU Film／Bloom入力の採取が0件のため、producer laneは同一drawの観測GPU定数を明示使用しています。過去のCPU算術49成分一致を今回の再検証へ加算しません。原作入力比較はBuilt-in D3D11の限定領域です。

最終ソースのmusic1001は **142秒で自然終端・Viewer復帰**。初回late-only1件を保留し、7,167/7,167更新を描画、左右同時45/45更新を確認しました。90 camera入力のうち88入力は現在Film・keywordで登録済みprogramを選択し616段を実行、同一catalog内のcache hitは87件です。対象pass外の2入力は保留、未登録programへの置換は0。GPU内部replayは7回・49段・**2,386,944/2,386,944画素RGBA一致**で、provider／render fault・例外は0でした。原作とViewerの同一frame画像の比較ではありません。

原作runはStopCompleted、保持handle復旧、hook復元・drain・pin照合、通常終了後の所有配備復元が成立しました。旧runの未完了readbackと過去Pendingは変更しません。現在は観測済みの単一programを登録する限定経路であり、他variant、全候補の適格性lifetime、自然global writer、中央camera、TextureMapping、音声epoch、copy shaderの生成identity、全画面RGBは独立した未確定事項です。全false／Unknown gateを維持します。

## 混合programのcache miss・追加・hitの実観測

追加の限定原作runで、対象shaderのselector、cache検索、近似検索の呼出しと戻り値を結びました。先行する1呼出しでは検索が不成立となり、近似検索が返したprogramと、挿入側の再検索後に保存されたentryが対応しました。同じrunの対象drawではcache hitを観測し、返されたprogram・holder・PS binding・全bytecode一致したPS生成objectまで照合しました。store命令のPCそのものをhookした結果ではなく、実呼出しの入出力と資格化した制御フローによる限定判定です。

要求されたkeywordは `MODE_VIGNETTE_ADD`、選択programの候補キーはそれに `COLOR_ONLY` を含むものでした。今回も `COLOR_ONLY` の有効化は観測していません。近似検索の選択結果と、その後のcache利用を別々に確認できたため、宣言keywordを実際の有効bitへ読み替えずに対応を説明できます。

先行するentry追加と対象drawのhitは、同じshader・group・検索キー・program・PS objectに対応します。ただし、追加時のcamera帰属、間の全invalidation／writer閉包、entryの連続lifetime、他の検索キー・全候補への適用は未証明です。前回runのsnapshotを遡って実行分岐の証拠には変更しません。

最終runは **StopCompleted**、同じ保持handleによる復旧、hook復元・drain・pin照合、通常終了後の所有配備復元が成立しました。新規の私有閉鎖検証はnative **18/18**、読取・判定model **8/8 PASS**。今回は補助の画像readbackが未完了なので、GPUで確認したのはPS bindingと生成objectのidentityです。原作RGB replay、公開Control結合、142秒のViewer自然再生は再実行しておらず、過去の成功に加算しません。公開変更は文書のみで、通常renderer・variant選択・既存APIは変更していません。

準備中に、先行VSを見た重複除外がPSまで除外する問題と、起動時の多数のpassで監視範囲を消費する問題を修正しました。shader単位の資格化、cold VS→PS順序と大量の対象外入力のfixtureを追加し、容量・読取上限は維持しました。別Unityと重複した閉鎖GPU試験は受入れから分離し、不在確認後に再検証しています。未起動の接続待ち失敗と途中原作runのPendingはそれぞれ保持し、最終runの成功へ置換していません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 有効bit・検索キー・選択候補・宣言の比較

Editor専用の `OriginalPostFilmRouteAudit.CompareProgramKeywords` を追加しました。有効keyword、runtime検索キー、選択されたprogramの候補キー、保存assetの宣言を、呼出し側が明示した4つの集合として比較します。検索キーと候補キーの差を、そのkeywordの有効化と読み替えません。program identityと同一更新の対応は別の証拠が必要です。

追加の限定原作観測では、混合drawの同一更新について、検索キャッシュ、選択program、holder、GPUのPS object、全bytecode一致による生成objectを結びました。有効集合とキャッシュの検索キーに `COLOR_ONLY` はなく、選択された候補キーと保存assetの宣言には存在します。確認した検索ループの採点と同点時の先頭優先を実候補表へ適用すると、観測された選択と一致しました。keywordの暗黙的な有効化を仮定せず、この対応を説明できます。

前回観測したのは同一更新のキャッシュとprogramの対応です。その前回drawでcache hit／missのどちらを実行したか、以前にそのcache entryを作った呼出しは直接traceしていません。VSとPSは実holderのstageで区別し、runtime indexと保存variant番号の一致だけをidentityの根拠にしません。他family、全候補、同一原作frameのViewer RGB一致は別の未確定事項です。

null入力を空集合で補完せず、重複・不正token・上限超過を拒否します。入力の並べ替えと複製により、後から呼出し側の配列が変更されても結果を保持します。Unityの選択規則を実装するAPIではなく、通常rendererの登録・keyword writer・pass選択は変更しません。

最終ソースの公開人工監査は **137/137 PASS**。従来のD3D11人工描画による **64/64画素RGBA一致** と、新しい集合の差・複製保持・拒否条件を確認しました。私有sourceのない公開Control結合も **376/376 PASS**。今回は原作RGB replayと142秒のViewer自然再生は再実行しておらず、過去の成功と合算しません。正常停止・復元と対象採取の成立を分け、途中runのPending・対象0件・未完了joinも保持しています。

## 実pass引数とCommandBuffer keyword順序

限定原作観測の7 drawについて、実行時pass引数を同一run・更新・一意なqueue範囲のGPU drawへ結びました。混合段は従来の候補passと一致しました。標準書戻しは全pass指定であり、実引数をliteralなpass zeroへ読み替えません。別の限定観測では、混合drawのpassと18 keywordの入力状態を同じconsumerへ結びました。保存assetの宣言keyword集合とは完全一致しないため、実program選択とkeyword未指定時の処理は分けて判定します。

最終runでは、その混合drawについて、実pass・keyword入力・全bytecode一致したPS生成object・GPU消費を同一runで結びました。保存asset内では実passに対応するPS aliasが一意です。これは単一drawのPS対応であり、keyword選択の内部分岐、VS／sampler全体、他のvariant、同一frameのViewer RGB一致まで確定した結果ではありません。宣言keyword名を実際の有効bitへ読み替えず、保存候補を通常ライブへ自動登録しません。

`OriginalPostFilmRouteAudit` に、明示した開始global集合へ既存の設定／解除順を適用するEditor専用監査を追加しました。MODE／blendはCommandBufferへ積むglobal命令なので、materialのkeyword一覧だけでは描画時状態を説明できません。開始状態が欠けた場合を空集合で補完せず、material localとglobalを別に扱います。通常rendererへの登録やkeyword writerの追加はありません。

公開人工監査は **97/97 PASS**。既存 `Render.Blit` の2 drawを実行するD3D11試験で、pass選択・保持global・独立したmaterial keywordの組合せを検証し、**64/64画素RGBA完全一致**。公開Control結合も **376/376 PASS** でした。原作の同一frame RGB比較や142秒自然previewを再実行した結果ではありません。全false／Unknown gateと過去Pendingを保持します。

## 演出keyからpass／keyword候補への対応表

保存したmusic1001のFilm252 keyとBloom／Diffusion108 keyを、frame・layer・authored multi-camera番号・mode・color type別のローカル表へ整理しました。Filmのserialized payload252件は復元前後で一致しています。境界前後と区間内の **918サンプル**を実Controlの選択・補間処理へ渡し、全件で評価値を取得しました。うち332件は補間を使用しています。明示audit cameraと保存typed dataのreplayであり、自然再生や原作時刻との同期ではありません。

`OriginalPostFilmRouteAudit` は、評価済みFilmと明示base passから、現在Viewerコードの有効判定、inverseによるpass加算、MODE keywordの設定／解除、blend keywordの解除範囲を説明するEditor専用APIです。既存の有効判定とkeyword配列を参照し、描画・keyword変更・renderer登録は行いません。保持されるkeyword、capability owner、material availability、default/copy経路は別に資格化する必要があります。

選択shaderの保存metadataにある276個のVS／PS aliasを検査し、program・pass・keywordの対応表へ結びました。918件のうち130件はFilm描画なし、788件はFilm有効でした。後者のうち772件はBloom／Diffusion両有効の条件下で保存資産内の候補variantが各1件に絞れ、16件はその条件外として別経路を保留しています。候補が1件でも、必要な `COLOR_ONLY` やstereo等の実状態、Unityが選択したvariantを確認したことにはなりません。color typeからkeyword writerを推測していません。

過去runの保存証拠を再照合し、先行5 drawでは同一runの全bytecode一致した生成objectとGPU消費を結び、保存資産内のpass候補を各1件へ絞りました。実materialへのpass指定を直接観測した証拠とは区別します。旧cacheの上限到達、混合drawと最後のcopyのprogram identity Unknownは維持します。後続の別runにあるCPU inverse値からもコード上の混合pass候補を絞れますが、その別runを旧生成objectへ結合していません。

公開人工試験は **56/56 PASS**、選択metadata parserの閉鎖試験は **10/10 PASS**、私有source退避後の公開Control結合も **376/376 PASS**。PSだけを限定採取する追加run案は、既存の閉鎖GPU fixtureが拒否したため配備前に停止し、controller・ゲーム起動・ARMは0でした。上限や試験を緩和していません。PS取得だけでは共有passの区別が残るため、次の実測対象はmaterialのpass指定とkeyword設定を同じdrawへ結ぶ境界に限定します。

通常renderer、保存候補programの採用条件、自然preview、Coreは変更していません。原作同一frame RGBの追加比較や142秒自然再生は今回は行っていません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## ライブデータを起点とした対応付け

調査順序を **ライブ設定・typed timeline → Prefab／Material／Shader参照 → 保存済みCPU／GPU観測 → 残ったruntime依存だけ採取** へ変更しました。追加の原作runは行っていません。既存rendererや現在property取得APIの動作は保持します。

music1001のローカル依存78 bundle、約23.8MBを既存のasset managerで選択ロードし、キャラクター・音声・ライブを実行せずに調べました。import済みworksheetのkeyリスト225本・5232 key、Prefab 9件、Material 22件、componentからのobject参照91件をローカル専用に保存し、DBと対象AssetBundleの監査前後のhash一致を確認しました。これはViewerのtyped schemaから読める範囲の一覧で、未定義のraw fieldを含む原作データ全体の完全性を証明しません。

Bloom／Diffusionは108 key、Filmは通常3層と左右multi-cameraを合わせて252 keyでした。左右位置の各6 keyと、multi-camera番号を持つFilmの各3 keyを、同じデータの別リストとして保持しています。Filmのmovie参照はこの選択データでは0件でした。時刻・色・強度・曲線・resource識別子などの実値は公開repoへ含めません。

関連shader資産からDiffusion系7 pass、FastBloom系5 pass、宣言keywordと既定propertyを取得しました。保存7 drawのうち6 drawは、この2 shader資産と名前が対応します。最後の書戻し名は現在Editorの組込みshaderにも存在しますが、原作の生成API identityや実際のpass／variant選択を確定したことにはなりません。Dimmerのasset既定値4成分は以前の原作保存既定値とbit一致しましたが、可変writerの証明とは分けます。

Prefabの未解決component slotは3件です。階層上の所在を限定しましたが、EditorのMissing MonoBehaviour判定は0件であり、3件をmissing scriptと断定しません。型・field・動作はUnknownです。次はこの不足と、Film指定からpass／keywordを選ぶ境界をデータ・既存コードで詰め、同一更新の実行証拠が必要な箇所だけを採取候補にします。

公開の `OriginalLiveDataInventory.Capture` はロード済みtyped dataを読み取るEditor専用APIです。元のkey順序、nullと空、worksheet対象camera、明示されたmulti-camera番号を保持し、key選択・補間・runtime登録を行いません。payload取得は明示opt-inで、件数・深さ・総量に上限があります。人工検証は **22/22 PASS**、私有source退避後の公開Control結合も **376/376 PASS**。以前の142秒再生・RGB一致は独立した既存成果として保持し、今回の再実行とは扱いません。[使用範囲](Tools/OriginalLiveRendering/INTEGRATION.md#typedライブデータの読取監査) を参照してください。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## UV scroll／Dimmerの現在property供給

`OriginalPostProcessPropertyState` を追加しました。明示materialのColor／Vectorと、別に指定したglobalの現在値を同期取得し、camera、selected／render serial、入力textureへ結びます。非有限値、materialの解放・shader変更、微小なproperty変更、古い更新や別入力を拒否します。getterは状態を変更せず、通常UIや既存rendererへ自動登録しません。[供給条件](Tools/OriginalLiveRendering/INTEGRATION.md#現在materialとglobalの対取得) を参照してください。

原作の限定観測では、UVの実writerのowner field、global setter、現在global、同じmixed drawのGPU消費を対応付けました。Dimmerも正式なproperty名からshader既定state・現在material state・同じdrawのGPU定数を結び、消費する計6成分がfloat32 bit一致しました。観測したUVはゼロ、Dimmerは既定色であり、非ゼロUVや可変Dimmerの自然遷移、ownerの型・全lifetimeを証明したものではありません。

公開人工GPU試験はBuilt-in／URP14で **416/416 PASS、2240/2240画素RGBA一致**。異なる2更新のglobalによるUV移動とmaterial色変化を実GPUで確認しました。私有source退避後の公開Control結合も **376/376 PASS**。今回の原作保存入力では、property **6/6**、draw **248/248**、既存producer **49/49**成分がbit一致し、独立7 draw **896/896**、連続中間 **896/896**、最終 **128/128画素RGBA一致**、invalid sample 0でした。独立・連続比較は同じ原作観測の再評価です。

最終ソースの自然再生は **142秒、7939/7939描画、左右同時58/58更新**で終端・Viewer復帰しました。116 camera入力のうち候補Film条件が成立した114入力で現在propertyを取得し、保存effect vector使用0、**798段**、内部replay **2727936/2727936画素RGBA一致**、fault／例外0でした。残る2入力は保留です。Dimmerはロード済みshader資産から作成したmaterialの現在値、UVはViewerの現在global getterから供給しています。getterだけではglobalの存在やViewer内writerを証明できず、この再生のUVはゼロでした。原作同一frameの自然RGB一致には拡張しません。

対象programの87個の段別unique register参照は、既存producer／draw85と今回の現在property観測2に分類します。保存effect contextへの依存は除去しましたが、後者を上流producerや更新owner全体の資格化、全定数の自然生成率とは扱いません。追加5原作runはそれぞれStopCompleted・保持handle復旧・通常終了後の所有配備復元を確認しました。未使用のmanaged thunkで書込み0だった診断や、略称propertyで名寄せできなかった採取を、writer不存在・Dimmer成功へ読み替えません。容量超過案はrun開始前の拒否として分離しています。

projection生成元、variant／sampler、自然TextureMapping、中央下地camera、音声epoch、最後のshader生成API identity、原作同一frameの全画面RGBは未確定です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 書戻しtintの現在material供給

書戻しtintを、保存値から現在のmaterial propertyへ置き換えました。原作の限定観測では、標準コピー用material、shader既定state、現在state、実際の書戻しdrawを同一frame・同一queueの一意な命令範囲で対応付け、名前で特定した色propertyの4成分がPS定数とbit一致しました。初期化のCPU処理と組込みshader資産も確認しています。新しいshader生成APIの同一run identityや、全コピー経路の資格化には拡張しません。

`OriginalPostProcessDrawState.Capture` に、呼出し側が所有するmaterialとproperty名を渡すoverload、nullableな `MaterialVector` を追加しました。現在のraw vectorを同期取得し、欠落・型不一致・非有限値を拒否します。使用直前にはframe／入力に加え、materialの解放、shader変更、property値の変更を検出します。従来overloadでは値を補完せず、`MaterialVector` は未指定のままです。色空間変換は推測しません。

公開人工GPU試験はBuilt-in／URP14で **314/314 PASS、2112/2112画素RGBA一致**。shader既定値、非白色の明示値、alpha、ゼロ値と拒否条件を確認しました。私有source退避後の公開Control結合も **376/376 PASS**。今回採取した原作入力では、draw情報248成分・CPU producer49成分がbit一致し、独立7 draw896画素、連続中間896画素、最終128画素のRGBA一致を確認しました。独立処理と連続処理は同じ観測の再評価です。

最終ソースの自然再生は **142秒、7437/7437描画、左右同時53/53更新**で終端・Viewer復帰しました。対象106 camera入力すべてに現在materialからtintを供給し、保存tint使用0、7段742回、内部replay2727936画素RGBA一致、fault／例外0でした。原作の非白色tint遷移や、自然再生同士の同一frame RGB一致を示すものではありません。

87個の段別unique register参照は現在producer／draw85、明示retained 2です。残るeffect globalはUV scrollとDimmerで、全定数の自然生成率を表しません。最初の追加runはreceiver schema登録漏れでPendingとなり、記録を保持したまま通常終了後に所有配備を復元しました。専用validatorと拒否試験を追加した修正runはStopCompleted、保持handle復旧、hook復元・drain・pin照合、通常終了後の所有配備復元まで成立しました。

通常UI、coverage、camera選択、cloth、postprocessor未登録時の経路は保持します。投影生成元、variant／sampler、自然TextureMapping、中央下地camera、音声epoch、最後のshader生成API identity、原作同一frameの全画面RGBは引き続き未確定です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 書戻しUVの現在draw供給

残っていた4参照のうち、書戻しUV scale/offsetを現在のdraw引数から供給する接続を追加しました。原作側では呼出し元の限定命令、同一buildの内部登録先と実コード先頭、標準Blitの命令記録を照合しています。内部関数の登録先照合は、その関数の呼出しを新たに観測した証拠や、最後のshader生成API identityの証明には拡張しません。

`OriginalPostProcessDrawState` に明示UV引数のoverloadと `UvScaleOffset` を追加しました。既存overloadは単位scale・ゼロoffsetを持ち、既存の行列／texel計算と入力検証を維持します。通常ライブへの自動採用、coverage shader、camera選択の変更はありません。

公開人工GPU試験はBuilt-in／URP14で **234/234 PASS、1920/1920画素RGBA一致**。単位変換に加え、負scaleによる反転、部分切出し、明示Y反転、異なる入力寸法、非有限UVと古いframe等の拒否を確認しました。私有の保存原作入力では、今回のUVを含むdraw情報 **244/244成分bit一致**、既存CPU producer **49/49 bit一致**、独立7 draw **896/896**、連続中間 **896/896**、最終 **128/128画素RGBA一致**でした。同じ保存観測の再評価であり、新規観測数として合算しません。

最終ソースの自然再生は **142秒、7910/7910描画、左右同時60/60更新**で終端・Viewer復帰しました。120 camera入力中、候補Film条件が成立した118入力へ現在drawのUVを供給し、保存UV使用0で826段を実行しました。対象外の2入力は保留です。8回・2727936画素のGPU内部replayは全RGBA一致、renderer/provider faultと例外は0でした。原作の同一frameとの直接RGB比較ではありません。

対象programの87個の段別unique register参照は、現在producer／draw由来84、明示retained 3となりました。残る3参照はUV scroll、Dimmer、書戻しtintです。これは参照の分類であり、全定数の自然生成率ではありません。型やmethodの限定採取でこれらのwriter不在を証明したことにはせず、ゼロや白で解決済みにしません。追加した3原作runはそれぞれStopCompleted、保持handle復旧、hook復元・drain・pin照合、通常終了後の所有配備復元まで成立しました。

projection生成元、variant／sampler、自然TextureMapping、中央下地camera、音声epoch、最後のshader identity、原作同一frameの全画面RGBは引き続き未確定です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

私有source撤去後の既存Control結合も **376/376 PASS**。

## MipBiasのproducer接続

原作の対応methodを限定metadata採取へ結び、幅比のfloat32演算、double対数、float32への変換、temporal値とのmin、gain生成の順序を確認しました。`OriginalPostProcessSamplingMath` は確認した算術を明示入力から実行します。保存MipBiasを既定値として補いません。

`OriginalCameraFrame` には、targetが束縛されているprivate render cameraのpixel幅、現在のdynamic幅scale、URPの現在temporal値、stereo状態を追加しました。temporal値はsource cameraのcomponentや古いshader globalから流用せず、設定が存在しなければnullです。既存の描画経路・constructor引数は維持しています。

公開人工試験はBuilt-in／URP14で **53/53 PASS、256画素RGBA一致**。異なる色を持つmipを実際に選択し、float32除算の丸め位置、dynamic scale、temporal値、現在frameへの伝搬と不正入力の拒否を確認しました。私有source撤去後の既存Control結合も **376/376 PASS**。私有の保存入力比較では、明示した等倍・temporalゼロ条件で **24/24成分がbit一致**し、独立7 drawの896画素、連続中間896画素、最終128画素のRGBA一致を維持しました。24成分は同じ状態を各段で検査した件数で、原作の同一更新からCPU camera入力を直接採取した証拠ではありません。

選択programの87参照中、MipBiasの6参照を保存値からproducer算術へ移しました。残るCB上の明示retained参照はUV scroll、Dimmer、書戻しUV／tintの4参照です。83/87は全CB成分の自然生成率ではなく、参照単位の分類です。投影context、variant／sampler、自然TextureMapping、時刻原点と原作同一frameのRGB一致は別に未確定です。[供給条件と検証](Tools/OriginalLiveRendering/INTEGRATION.md#mipbiasの現在入力供給) を参照してください。

最終ソースで「うまぴょい伝説」**142秒**を自然終端・Viewer復帰まで確認しました。**7905/7905更新**を描画し、左右同時有効 **59/59更新**、fault／例外0、初回late-only 1回は別に保留です。118 camera入力全てで現在のURP temporal値と幅情報からsamplingを生成し、**826段**へ供給しました。保存MipBias使用は0件、8回の内部GPU replayは **2727936/2727936画素の全RGBA一致**です。この再生のbiasは0で、非ゼロbiasによるmip選択の検証は人工試験に属します。先行runのbackendガードによる全保留は成功扱いにせず、自然完走と7段処理の受入条件も分離しました。

今回の原作runはStopCompleted、保持handle復旧、hook復元・drain・pin照合、通常終了後の所有配備復元まで確認しました。過去Pendingを変更しません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## Fullscreen draw状態の再生成

`OriginalPostProcessDrawState` を追加しました。現在のcamera入力・render serial・selected serialへ結んだ状態から、明示したfullscreen用model／view／projectionと現在のtexture寸法を使い、GPU view-projectionと符号付きtexel情報を生成します。映像を生成したcameraの投影をfullscreen描画へ流用しません。既存renderer・chainの既定動作は保持し、明示した利用側だけが採用します。

私有の候補7段経路では保存CBの一括読込みを外しました。選択programの静的参照を全て分類し、必要なproducer値・draw状態・明示した未解決globalだけを設定します。保存入力で再生成したdraw情報の **240/240 float32成分がbit一致**し、既存CPU producerの49成分も一致しました。参照されない保存CBを捨てたGPU比較で、独立7 drawの **896/896画素**、連続した各中間段の896画素、最後の **128/128画素がRGBA一致**しています。同じ原作保存観測の再評価であり、独立した原作観測数へ合算しません。追加の原作runは行っていません。

この経路で「うまぴょい伝説」**142秒**を自然終端・Viewer復帰まで再検証しました。**7980/7980更新**を描画し、左右同時有効 **59/59更新**、fault／exception 0、初回late-only 1回の保留を確認しました。対象118 camera入力のうち116入力・812段でdraw状態を生成し、8回の内部GPU replayは **2727936/2727936画素の全RGBA一致**です。残る2入力は候補のFilm mode／color条件の対象外として別記録にしています。前節までと件数が同じでも別のrunであり、過去の記録を書き換えません。

公開人工試験はBuilt-in／URP14で **78/78 PASS、640画素RGBA一致**。入力サイズ・上下反転、古いserial、別camera／texture、解放・サイズ変更・非有限行列の拒否を確認しました。公開ソースだけの既存Control結合も **376/376 PASS**、wrapperの混在入力拒否は2/2です。

Fullscreen投影範囲・Y規約・meshは明示した候補contextです。原作のconstructor／writer全体を証明したものではありません。MipBias、UV scroll、Dimmer、書戻しUV・tint、variant／samplerも確認済み保存contextへの依存が残ります。原作の全自然global生成、自然TextureMapping、同一原作frameのRGB一致は未証明です。[APIと残る依存](Tools/OriginalLiveRendering/INTEGRATION.md#fullscreen-draw状態の生成) を参照してください。採取値・program・私有接続器・画像は公開しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## PostFilmの自然供給と候補7段への接続

`OriginalPostFilmMath`、`OriginalLivePostFilmProvider` とbridgeの明示opt-inを追加しました。実worksheetの通常camera用3層とmulti-camera用3層から、Controlが選択したkeyと実際に使った補間率を渡します。multi-camera番号は既存のposition trackへ一意に結合し、そのViewer cameraを使います。番号を配列indexへ読み替えたり、通常cameraのFilmで補いません。

scalar・色・roll・scaleを評価し、同じselected serial・camera・layerのFilmだけを既存CPU→GPU経路へ供給します。未通知、重複、別camera、古い更新は供給成功にせず、Movie／BlinkLight同期は未接続のままです。profile未登録時は従来経路を保持します。

原作の自然keyと評価直後payloadを同じ呼出しで採取し、判定できた **13/13件**で、**float32 390成分のbitと型付き値104成分が一致**しました。これらは補間しないselected keyの観測で、残る3件はcurve入力不足として直接照合から除外しています。自然curve遷移を原作と照合した結果ではありません。公開人工試験はBuilt-in／URP14で **33/33 PASS**、既存Control **376/376**、既存producer **44/44** も個別に再検証しました。

最終runtimeで「うまぴょい伝説」**142秒**が自然終端・Viewer復帰まで成立し、**7980/7980更新**を描画、左右同時有効 **59/59更新**を確認しました。Film観測は24061件、初回late-only 1回は別に保留しています。対象118 camera入力のうち **116入力・812段**へ自然color／depth／Bloom・Diffusion／Filmを供給しました。残る2入力は成立件数に含めません。

私有GPU検証では保存済み候補variantとfullscreen用global templateを明示し、人工7段式を使わず実programの経路を実行しました。8回の段別GPU replayで **2727936/2727936画素の全RGBAが一致**し、8回とも処理前後のRGBが変化しました。自然入力profileには保存RGB画像やFilm keyを含めていません。ただし、固定のglobal templateと限定variantを使用するため、原作の全自然global／variant生成や同一原作frameのRGB一致は未証明です。最後のshader identityもUnknownのままです。

今回の原作runは2件がStopCompleted・所有配備復元まで成立しました。先行のmetadata上限停止runはPendingを保持し、通常終了後の配備復元だけを別記録へ残しています。私有検証器の初回失敗も成功runと分けて保存しました。採取値・nativeコード・program・画像・receiptは公開対象外です。[接続APIと検証の境界](Tools/OriginalLiveRendering/INTEGRATION.md#postfilmの自然key供給) を参照してください。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 先行成果：原作CPUパラメータ生成とGPU定数の対応

限定runで実methodと描画用値型のmetadataを取得し、Bloom／Diffusionのvector生成と、movieを使わないPostFilmの定数設定を確認しました。classの保存値と描画用値型のlayoutは区別しています。対象mask描画で直近の限定記録を固定し、同一更新・receiver cameraが一致するCPU入力を一意に選びました。別cameraや先行更新の値を代用していません。

公開実装は `OriginalPostProcessProducerMath` と明示opt-inの `OriginalLivePostProcessParameters` です。確認済み依存を外部profileから受け取り、既存のcurrent color／depth・typed payload経路へ接続します。Filmは同じselected serial・cameraに属する明示状態だけを受け入れ、未供給時はUnknownのままです。このCPU算術追加の時点ではPostFilm key評価は未接続でした。現在の自然供給は上節を参照してください。

公開の人工入力試験はBuilt-in／URP14で **44/44 PASS、4096画素のRGB差0**。既存Control結合も現在のソースで **376/376 PASS**。同じ原作更新から選んだCPU入力を公開算術へ渡す私有試験では、対象7段へ供給する **49/49 float32成分がGPU定数とbit一致**し、その値を再生成した7 drawの **896/896画素がRGBA一致**しました。連続生成の中間896画素・最終128画素も同じ保存参照に一致し、独立した原作観測数へ合算しません。私有RGB試験はBuilt-in D3D11です。

採取runはmetadata取得、先頭限定記録、対象描画に合わせたリング記録を別々に保持し、いずれもStopCompleted・通常終了後の所有配備復元を確認しました。先頭記録だけでは対象更新に届かなかったため、その記録を同一更新比較へ採用していません。過去Pendingの判定は変更しません。

これは自然状態を原作CPUで採取した限定領域の再描画です。Viewerの実楽曲からPostFilm状態を連続生成するprovider、全global定数の自然生成、最後のshader生成identity、時刻原点、全画面RGB一致は未確定です。今回、142秒の自然再生は再実行していません。[APIと比較範囲](Tools/OriginalLiveRendering/INTEGRATION.md#確認済み後処理パラメータの生成) を参照してください。原作のprogram・定数値・layout・採取器・receiptは公開しません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 各段の入力更新と中間RGB比較

既存chainへ、描画ごとの定数準備、現在の外部textureの解決、描画直後の出力通知を追加しました。現在depthを前回の参照で補完せず、右・左cameraの各同期callback内で解決できます。通知先はRTが再利用される前にcopy・readbackします。従来の引数だけで呼ぶ経路は保持しています。

保存shaderの共通metadataに加えて選択variantのparameter情報を調べ、混合段のdepth・camera color・前段出力の参照を対応付けました。混合段にはBloom／Diffusion以外のPostFilm関連入力も必要で、現在の自然payloadだけでは全定数を生成できません。名前や保存値の近さから変換式を採用していません。

公開人工GPU試験はBuilt-in／URP14合計 **105/105 PASS、4352画素**。同じ更新のdepth・typed payloadを7段へ渡し、各UNorm書込み後のRGBをCPU式と比較しました。後処理の独立試験 **46/46** とControl結合 **376/376** もPASSです。保存原作入力の私有試験では、独立7 drawの **896/896画素** と、連続生成した7段の中間出力 **896/896画素** がRGBA一致しました。後者は同じ保存参照の再評価であり、新しい原作観測数へ加算しません。私有試験はBuilt-in D3D11です。

自然再生の追加probeも7段の人工演算を使います。これは段ごとの入力更新とRGB比較の診断であり、原作7段の自然CB生成ではありません。原作の追加runは行っていません。最後のshader identity、PostFilmの自然供給、Bloom／Diffusionからの複合定数生成、同一原作frameのRGB一致は未確定です。[APIと証拠の境界](Tools/OriginalLiveRendering/INTEGRATION.md#描画ごとの準備と中間出力通知) を参照してください。

現ソースで「うまぴょい伝説」142秒の自然終端・Viewer復帰まで再検証し、**7372/7372更新**を描画しました。左右同時有効 **58/58更新**、camera入力116回・812段が完了しています。7入力×7段の49比較、**6272画素**で人工式との差は最大1 byte、renderer／provider faultは0でした。初回late-only 1回は別に保留しています。前回7851更新とは別runで、更新件数の大小から非退行や原作同期を判定しません。私有の連続7段比較では、各段の比較領域への範囲外sampleと無効入力伝播も0を確認しました。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と過去Pendingを維持します。

## 自然depthとtimeline値の連続供給

明示opt-inのframe postprocessorを追加しました。今回描いたViewer cameraのcolor・raw device depth、render serial、selected serial、view／GPU projection、near／farを同じcallbackへ渡します。実Controlがその更新で発行したBloom／Diffusion payloadもbridgeに値として保存し、selected camera・owner観測・serialが揃った場合だけ消費します。未通知の値を前回から補完しません。

「うまぴょい伝説」の実楽曲142秒が自然終端・Viewer復帰まで成功し、**7851/7851 owner更新を描画**しました。左右同時有効 **63/63更新**、camera入力 **126回**でdepthとtyped parameterを連続供給し、parameter変化2回も観測しています。7回の限定GPU readbackで **896画素**を人工RGB式と照合し、最大差1 byte以内でした。実geometryを含む非far depthも確認しました。これは今回追加した供給経路の検証で、前節の原作保存入力896画素完全一致とは別です。原作7段処理への自然な全定数の変換や原作RGB一致は確定しません。

追加人工試験はBuilt-in／URP14合計 **45/45 PASS、768画素**。上下で異なるdepth、更新ごとのclip値、typed payload、古いserial／欠落値の拒否を確認しました。既存の32件と376件も個別に再検証しています。[APIと比較の境界](Tools/OriginalLiveRendering/INTEGRATION.md#自然cameraのdepthとtimeline-payload) を参照してください。

最後のshader identityはUnknownです。同じ候補を生成し直して既存shader参照へ結ぶ案は、閉鎖ホストで別objectになるため不採用としました。新しい原作runは行わず、前回の生成API照合・保存RGB再現・停止判定を保持しています。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` は変更しません。

## 後処理の明示接続と保存RGB比較

`OriginalPostProcessChain` を追加し、明示したMaterial・Mesh・texture参照から複数段の中間RTを生成できるようにしました。`OriginalCameraProducer.SetInputPostProcessor` と連続rendererの同名APIで、camera描画後・mask入力前へ接続します。未登録時の経路と中央下地の描画は従来どおりです。通常ライブへの自動登録は行いません。

新しい原作runでは、7 drawの全定数buffer、実頂点宣言・vertex/index、入力画像の限定領域、depth、描画直後の参照領域が揃いました。私有の候補programをこの公開chainで実行し、各drawの保存入力による **896/896画素のRGBA一致** と、7段を連続生成した最後の **128/128画素のRGBA一致** を確認しました。後者は最終drawと同じ参照領域の再評価で、独立した原作観測ではありません。参照範囲外のsampleと前段からの無効入力伝播も比較領域では0です。

最後の書戻しを含め、候補VSの静的定数参照を全て保存範囲内へ収め、実頂点と補間を使用した限定RGB再現が成立しました。ただし、最後のVS／PSをshader生成APIへ同一runで結ぶidentityは引き続きUnknownです。RGB一致からbytecode／variantの一意性を確定しません。これはWindows D3D11の保存領域の再描画であり、全画面・連続時刻・自然depth／定数供給や、原作camera映像そのものの生成一致は未証明です。

公開人工GPU試験はBuilt-in／URP14合計 **32/32 PASS、4096画素**。既存公開結合試験も現コードで **376/376 PASS** です。私有RGB試験はBuilt-in D3D11で実行しました。過去の1868件・142秒自然再生を今回の実行件数へ加算しません。最終採取runはStopCompleted、保持handle復旧・hook復元・drain・pin照合・通常終了後の所有配備復元を確認しました。途中の入力不足runと過去Pendingは別記録を保持します。

公開するのは汎用接続、人工shader・検証、説明だけです。原作program・入力値・画像・採取コード・receiptは公開repoへ含めません。[接続方法と比較条件](Tools/OriginalLiveRendering/INTEGRATION.md#明示した後処理chainの接続とrgb比較) を参照してください。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 後処理の実shader生成と描画参照を接続

原作のshader生成APIへ渡された候補bytecodeを全byte比較し、成功時に返ったshader objectを保持して、同じrunの実drawが参照したVS／PSへ照合しました。shader名や異なるrunのポインタ一致による推定は使っていません。

正常終了した限定runでは、7 draw中、先頭の拡散2 drawとbloom3 drawの **10/14 stage** を接続できました。追加runの保存データでは複数入力の拡散drawまで **12/14 stage** を照合していますが、この追加runは読取上限による **PendingManualRecovery** です。データ照合と停止成功を区別し、元の判定を保持します。最後の書戻しdrawのVS／PSはUnknownです。

7 drawのVS定数prefixと、複数入力drawに対応する限定depth gridも保存しました。実bytecodeの逆アセンブルから、確認済みPSの静的定数参照は保存範囲内、一方でVSには未保存の定数参照があると判明しました。同じbytecodeに複数のpass／keyword別名が対応する場合、その選択まで一意には確定しません。[照合範囲と再描画に不足する入力](Tools/OriginalLiveRendering/INTEGRATION.md#実shader生成との同一run照合) を参照してください。

採取器の最終閉鎖検証 **1006/1006**、関連受信側 **36/36** がPASSです。先行3 runのStopCompletedと、追加runのPendingを分離して保存しました。全runで通常ゲーム終了後の所有配備復元を確認しています。Pending runについて実行中の保持handle復旧成功は主張しません。

公開変更は文書のみです。Viewerへの後処理実装・RGB replayは未実施で、既存の376/376・1868/1868と142秒自然再生を今回の再検証とは扱いません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## Resolveから右maskまでの描画内容を採取

内部owner時刻で同期した新しい限定runで、対象Resolveから右mask消費までに観測した **7 draw** と消費maskを保存しました。各drawのshader名・参照texture・pixel shader定数・sampler・描画前後の画素gridを、同じ区間のAPI記録へ照合しています。従来の「4回」は共有入力RTに直接触れた描画の数で、途中RT上の描画を含む全pass数ではありませんでした。

拡散系の名前を持つ2 draw、bloom系の3 draw、複数入力を持つ拡散系の1 draw、入力RTへの書戻し1 drawを観測しました。保存gridでは各描画のRGB変化を確認し、最後の書戻しは同じ整数画素位置の入力・出力が一致しました。shader名だけで実bytecodeやpass番号を確定せず、全画素の演算一致へは広げません。[保存範囲と不足入力](Tools/OriginalLiveRendering/INTEGRATION.md#resolveから右maskまでの描画内容) を参照してください。

最終runはStopCompleted、hook復元・drain・pin照合・保持handle復旧・通常終了後の所有配備復元が成立しました。採取器の閉鎖検証 **796/796**、関連受信側 **57/57** がPASSです。途中runの元Pendingと、過去のViewer **376/376・1868/1868** は独立して保持します。今回の公開変更は文書のみです。

描画キュー位置・全writerのcamera帰属、実bytecodeの同定、頂点側補間、depth画素と自然なRGB入力供給は未確定です。既存rendererのRGB処理は保持し、`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 内部データによる採取同期

原作側の採取開始を、字幕画像の照合から、同じownerの内部時刻・cue比較値を確認する有限待機へ切り替えました。対象到達時にはowner、選択receiver、cue比較値、setter前後の時刻安定性を照合します。描画hookの有効化で区間が進むことを実測したため、手前の内部checkpointで準備し、対象では採取受付だけを開始します。この方式で左右の消費drawを実機取得し、正常停止・hook復元・保持handleによる復旧を確認しました。

歌詞データにはミリ秒の時刻表があり、Viewerの既存処理は同じライブ時刻をtimeline更新と歌詞表示へ渡しています。字幕を画面から読み取る必要はありません。ただし、原作の歌詞表示が使うclockと音声epoch、原作とViewerの時刻原点まで確定した結果ではありません。[内部同期の範囲](Tools/OriginalLiveRendering/INTEGRATION.md#内部時刻と歌詞時刻表による同期) を参照してください。

前回の内部同期成功runと、その時点の後処理追加runは別判定です。後者は記録が1件に留まり、内部slotの前後一致も不成立だったため、元のPendingを保持します。今回の新しい区間採取の成功によって、過去runの判定を変更しません。

採取器と実値はローカル専用で、今回の公開変更は説明文書のみです。既存のViewer renderer・通常UI・歌詞処理を変更せず、過去のPendingと `OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。

## 右Resolve前writerのcamera帰属と後処理

原作の新しい限定runで、右Resolve前の保存writer4件を、一意のrender-task・保持context・同じrunの右receiverのcameraへ接続しました。左右の自然readerと消費drawも同じ実更新で取得しました。同じResolveが二つのcamera記録へ付いていても、独立した二観測には数えません。

当時の記録では、Resolveから右mask drawまでに共有入力RTへ直接関わる4 drawと書戻しを観測しました。上記の新しい区間採取は途中RTの描画も含む7 drawを保存しています。**保存writerのcamera帰属は成立、Resolve結果の無変更消費は不成立**です。後処理の全writer・camera帰属は未証明で、後続の左Resolveへ右versionを流用しません。[段階別の判定](Tools/OriginalLiveRendering/INTEGRATION.md#右resolve前writerのcamera帰属と後処理) を参照してください。

字幕の背景依存を通常再生で再現し、判定基準と待機時間を保ったままmaskを追加画像で資格化しました。別の通常再生と実採取で同期を確認しました。採取中に判明したcamera ID位置とcallback／task／reader順序の誤りも訂正し、隣接値・欠落・重複・未完了の負例を追加しました。最終native閉鎖検証630/630、字幕36件、補助24件は独立した検証であり、Viewerの376/376・1868/1868へ加算しません。

最終runはStopCompleted・保持handleによる復旧・通常終了後の所有配備復元を確認しました。UI画像保存の容量上限は別判定です。以前の未ARM Pendingと途中runの未達範囲は書き換えません。今回の公開変更は文書のみで、既存のchannel別camera選択を維持します。最終mask入力の全面的なcamera帰属、中央下地、自然TextureMapping・投影・MipBias・owner epochと各false判定は未確定のままです。

## 左入力のcamera帰属を限定確認

新たな原作の限定観測で、左入力のResolve前に保存したresource writerを、同じ描画キュー範囲のrender-taskへ一意に接続しました。taskが保持するculling contextの一致とcamera IDを確認し、同じrunの左receiverが参照するcameraと一致しました。観測したwriter部分についての帰属であり、画像に寄与した全描画の帰属を証明するものではありません。

同じResolve記録が左右のcamera記録へ付いていても、独立した左右の観測として数えません。この左限定runの対象は右drawより後、左drawより前の共有入力versionです。当時未採取だった右側は上記の別runで保存writerの帰属を確認し、中央下地はUnknownを維持しています。[帰属の条件と適用範囲](Tools/OriginalLiveRendering/INTEGRATION.md#左入力のcamera帰属と共有rtのversion) を参照してください。

既存Viewerは左右それぞれのSelectionのcameraを使用します。今回の結果は左の限定観測とその方針の対応を支持しますが、camera投影・RGBや自然TextureMappingの一致、原作値の連続供給を新たに実装した結果ではありません。公開変更は文書のみです。直前の376/376・1868/1868は過去の成功として保持し、今回の再実行件数へ加算しません。

今回の最終原作runは正常停止・hook復元・drain・pin照合・通常終了後の所有配備復元を確認しました。別のUI画像保存は既存の容量上限で終了しており、UI観測全体の成功とは扱いません。過去のPending判定と各false／Unknownは維持します。

## 保存された原作drawとのmask alpha比較

原作の同一drawに属する入力宣言・index／vertex・定数upload・描画後画像を照合し、選択したdrawのUV/STとY反転条件を既存adapter→coverage→GPUへ明示入力しました。新しいEditor検証器は元RTの整数画素中心を保持して有限gridを読み、原作の描画後alphaと比較します。画像をgrid解像度へ縮小して描き直す方法とは区別します。

公開人工入力は **376/376 PASS**、保存実測を含む結合検証は **1868/1868 PASS**。Built-in／URP14で、右は明示した初期alpha 0のpolicy下で最大1byte差、左は保存した原作の右alphaを下地として一致しました。RGB・入力alpha・試験用sample biasを変えても比較alphaが変わらないこと、誤ったgrid方向を検出することも確認しました。新しい原作runは不要でした。

これは選択したdrawのmask alpha比較です。RGBは人工入力で、元のMipBias、画面座標からmesh UVへの正式な投影契約、camera帰属、自然mapping stream、owner epochを確定しません。今回のsourceでは自然ライブの長時間再生を再実施していません。既存の自然再生成果とは別に保持します。[入力形式と比較の境界](Tools/OriginalLiveRendering/INTEGRATION.md#保存drawのmask-alpha比較) を参照してください。

## 左右mask描画後の中間RT採取

既存の限定Editor観測へ、RightMaskDrawn／LeftMaskDrawn直後のAccumulation copyを追加しました。左右coverage・下地・最終出力と同じ更新の6画像に、処理段階・serial・入力version・RT descriptor／sRGB・PNG hashを保持します。左右の中間RTもreplayの同じ段階と全RGBA比較し、元RTの次更新による再利用に対する保存の独立性を確認します。[保存段階と検証範囲](Tools/OriginalLiveRendering/INTEGRATION.md#mask描画直後のaccumulation保存) を参照してください。

公開人工入力は **358/358 PASS**、保存実測を含む結合検証は **1840/1840 PASS**（従来から追加20件）。Built-in／URP14で左右の描画後RGBA、6画像の段階・version・descriptor、PNG hash／decode、次更新後の保存不変性を確認しました。

実楽曲も自然終端・Viewer復帰まで完了し、左右Accumulationと最終出力はreplayの対応段階と全RGBA一致しました。保存したM・D・Oから、原作で観測したbyte合成式のRGBと下地alpha保持も全保存画素で再確認しました。同一段階の演算比較であり、原作との同一frame・画像一致には昇格しません。

原作のmask描画後画像と比較するための観測点です。原作camera帰属・TextureMapping・色空間・owner epoch・後段FlipScreenの未確定点と、各false／Unknown判定を維持します。

## 二channelの自然供給と3分割preview

既存の連続providerへ、明示順序を持つ実楽曲の二channelを接続しました。有効keyの同時選択、同じowner時刻、異なるViewer camera参照を保持し、左右coverage→下地→最終合成を同一更新で観測します。公開人工入力は **338/338 PASS**、保存実測を含む結合検証は **1820/1820 PASS**（従来から追加26件）です。

ローカル実楽曲を通常の終端・Viewer復帰まで自然再生し、二channelが同時有効な各更新を描画しました。同じ更新で保存した左右coverageと下地からの再合成は最終出力と全RGBA一致し、左右・下地それぞれのRGB寄与と三領域も確認しました。修正後の同じsourceで、従来の単一channel・正duration fadeを含む自然再生も終端・復帰まで再確認しました。原作の追加起動・ARM・採取は不要でした。

追加のViewer preview policyは「無効key・解決済みfade 0・pass 0」のchannelを透明submissionにできます。kind 0の未知の厚さはsnapshotとLastUnresolvedに残し、有効key、fade未取得、別passには適用しません。既存constructorは従来の保留を維持します。[自然再生と同一更新の証拠](Tools/OriginalLiveRendering/INTEGRATION.md#二channelの自然再生と同一更新の証拠) を参照してください。

完全replayは全RGBAで比較し、各cameraの可視寄与はRGBだけで判定します。destination alphaの差を可視寄与に含めません。原作TextureMapping・S生成camera帰属・時刻原点は引き続き未確定です。

## 自然なkey・owner時刻の連続供給（前回）

`OriginalLiveContinuousProvider` がロード済みControlの各更新を受け取り、選択済みkeyと同じowner時刻の組から既存resolverを呼びます。無効keyも通知し、owner時刻を1更新で使い切ることで、初回・late updateだけの再実行ではUnknownを保ちます。停止・seek、登録所有権、未取得時の画素保持、単一／二channelを含む追加64件により、公開人工入力は **312/312 PASS**、保存実測を含む結合試験は **1794/1794 PASS** です。

明示callback付きの既存楽曲loaderから、単一trackのローカル実楽曲をモデル・舞台・音声とともにロードし、通常Director更新で生成するViewer previewへ接続しました。**通常の楽曲終端・scene復帰まで自然再生PASS**。native物理は初期化前の明示opt-outを使い、既存UIの既定動作は保持します。[接続・再生受入](Tools/OriginalLiveRendering/INTEGRATION.md#自然な連続供給とviewer再生) を参照してください。

このpreviewのTextureMappingは、Viewerが生成するfull-viewport・mipなしRTの明示設定です。原作の自然TextureMappingやcamera正式帰属へ昇格しません。原作からの時刻stream、原作とViewerの時刻原点・音声epoch、原作最終画素の全面一致は未成立です。各false判定と `Phase257cause=Unknown` を維持します。

## 自然な正duration keyのfade接続（前回）

別楽曲の有効keyから期間内15点・終点1点を自然採取し、実owner時刻・比較整数・raw durationを使う生成fadeが原作setter入力とbit一致しました。同じ実keyを選択する実Control → resolver → GPUもBuilt-in／URP14の全RGBAで一致し、従来1524＋追加206の **1730/1730 PASS**。公開用の人工入力は **248/248 PASS** です。[検証範囲](Tools/OriginalLiveRendering/INTEGRATION.md#正のdurationを持つ自然keyのfade) を参照してください。

今回の原作runは独立して正常停止・通常終了・所有接続復元を確認しました。過去のPending記録は変更しません。自然fadeの実測は1keyの限定区間であり、原作とViewerの時刻原点、自然TextureMapping・camera、楽曲通しの受入は別の未確定点です。各false判定と `Phase257cause=Unknown` を維持します。

## 実楽曲cueの角度生成と自然fade条件

保存した実楽曲のtyped cueとauthored curveを実Controlへ渡し、確認済みprofileから生成したshader角度を独立したreceiverの保存値と照合しました。184観測の角度2成分がすべてbit一致し、Built-in／URP14の人工GPU画像も全RGBAで一致しました。従来228件を含む **1524/1524 PASS** です。[入力・検証・制約](Tools/OriginalLiveRendering/INTEGRATION.md#保存した実楽曲cueからの角度生成) を参照してください。

対象の保存keyには正のdurationCountを持つ有効keyがなく、自然fade遷移は未証明です。今回の追加原作runはありません。観測元の元Pending判定を維持し、別runのowner時刻と結合していません。camera geometry・描画色・TextureMappingは人工入力で、通常ライブの通し再生を示すものではありません。

## owner比較入力の接続

ownerの現在時刻と比較時刻に対する保存済みproducer演算を確認し、明示opt-in時の実 `AlterUpdate` から既存resolverへ接続しました。実採取したowner入力を外部から与える検証は **228/228 PASS**。従来175件と追加53件を含み、Built-in／URP14で人工geometry・typed keyからの全RGBA一致を確認しています。[接続と受入範囲](Tools/OriginalLiveRendering/INTEGRATION.md#owner比較値の接続受入) を参照してください。

通常ライブの自動採用、原作とViewerの時刻原点・音声epoch・最終画面frameの結合は未確定です。採取・検証器の失敗は保存証拠の再照合と区別し、元のPending判定を書き換えていません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false` を維持します。

## ブランチと本家

- 本家：<https://github.com/katboi01/UmaViewer>
- Fork：<https://github.com/henntaikusooyazi-creator/UmaViewer>
- ブランチ：`feature/original-live-rendering`
- 起点：本家master `07f82e9fa08f23c1cda3a9be3045a09372eae8bb`
- README推奨はUnity 2022.3.62f1、起点のProjectVersionは2022.3.62f3、URPは14.0.12。
- 本家へのPRは未作成。Forkのmasterは同期用とし、この変更は開発ブランチだけに置きます。

## 実装の所在

|機能|Fork上の実装|状態|
|---|---|---|
|右描画→共有入力更新→左描画→下地とのalpha合成|`Assets/Scripts/OriginalRendering/OriginalMultiCameraRenderer.cs`|移植済み|
|原作式によるcoverage生成|`OriginalMaskCoverageGenerator.cs` と専用shader|移植済み|
|選択済み／評価済みcue snapshot|`OriginalCompositeCueAdapter.cs`|移植済み|
|確定しているproducer算術|`OriginalCompositeProducerMath.cs`|移植済み。有効thicknessの明示依存による分岐を追加。未解決helperは明示入力|
|実Controlの選択結果通知|`OriginalLiveTimelineBridge.cs`|本家の新しいLiveTimeLine側へ最小差分で接続|
|独立所有cameraによる描画|`OriginalCameraProducer.cs`|移植済み。Built-in／URP14|
|順次描画sessionと入力resolver|`OriginalLiveRenderSession.cs`、`OriginalLiveDrawInputResolver.cs`|移植済み|
|ロード済みtrackの明示登録|`OriginalLiveTimelineRegistration.cs`|移植済み。kとjを独立保持|
|typed groupのComposite選択番号|`LiveTimelineMultiCameraPositionData.MultiCameraNo`|追加済み。旧データでは明示TrackBindingを使用|

初回移植はC# 9ファイル、shader 2ファイルでした。後続の明示provider・算術・連続previewは各節に記録しています。今回の既存ソース変更はbridgeの時刻寿命、Controlの無効key通知、loaderの明示callbackと物理opt-out、camera不在時の視線追従guardです。Director、scene、ProjectSettings、Packagesの保存内容は保持します。

研究用Viewerの既存 `CompositeThree` はそのまま保持しています。その実装の変更なしコピーを `Tools/OriginalLiveRendering/Validation/Legacy` に比較テスト用として含めます。既存の通常ライブ出力を置換する機能ではありません。

## 過去の確定成果（今回のFork再検証とは別）

以下は移植前の研究環境・分離Unity等での実証概要です。private入力や採取物は公開しません。

|成果|過去の結果|証拠の適用範囲|
|---|---|---|
|Sequential GPU replay|29チェックPASS|保存中間画像の再生|
|原作mask coverage|34チェックPASS|coverage生成|
|Cue adapter|40チェックPASS|明示／評価済み入力|
|Timeline bridge Built-in／URP|47チェックPASS|typed fixtureと明示camera|
|Real song track registration|21チェックPASS|保存された楽曲のtrack／keyと検証用geometry|
|統合済み分離Unity検証|171/171 PASS|元Viewerの通常ライブ受入ではない|
|原作実FとViewer値|184/184 snapshot一致|観測済みFとjのみ|
|原作8 float値|1,472/1,472 bit一致|選択・補間されたraw値|
|原作実測mask alpha|18,423/18,432完全一致|残9点は最大1/255差|

最後の2項目は原作実測との照合です。人工RGBのGPU fixture検証とは区別します。新しいForkでこれらのprivate入力を再実行したという意味ではありません。

## 今回の検証

|今回実行した検証|結果|限界|
|---|---|---|
|Fork自体のUnity f3 import／コンパイル|未変更SliderControlのCS1010／CS1002で停止|Fork全体の受入は未完了|
|Forkから作成した分離Unity f3・D3D11|35/35チェックPASS|人工入力。通常ライブ・実Controlは対象外|
|engine側C# 5クラスと従来比較用クラス|分離Unityでコンパイル・GPU実行PASS|残るbridge等4クラスのForkでの全結合は未検証|
|新shader 2本と従来比較shader|コンパイル・GPU実行PASS|非公開shader binaryは使用しない|
|coverageの独立式照合|512サンプル、誤差1e-5未満|原作alpha残差9点の再照合ではない|
|順次描画と再利用|右・左・下地alpha保持、次frameの共有RT再利用PASS|人工RGB／alpha|
|Built-in／URP14のcamera描画|両経路PASS|背景色による明示camera入力|
|source camera／RT保護|状態とbuffer内容保持、Built-inのSetTargetBuffers保持PASS|source固有post effect等は対象外|
|従来CompositeThree|変更なしコピーのGPU smoke PASS|研究環境の過去受入全体の再実行ではない|

scene・ProjectSettings・Packagesは変更せず、原作ゲームも起動していません。公開用の人工入力テストは [実行方法](Tools/OriginalLiveRendering/README.md) を参照してください。

## 状態の区別

|段階|状態|
|---|---|
|原作解析でのcue・mask・実draw・合成構造|過去の限定実測で確認済み|
|ローカル研究Viewerの部品実装|実装済み|
|分離UnityでのGPU検証|上記の過去結果を保持|
|Forkへの移植|9クラス・2shader・最小timeline接続を実施|
|公開済み描画ブランチのUnityプロジェクト全体|本家由来の構文エラーでコンパイル未完了。7コミットの状態を保持|
|独立検証ブランチのmanaged／Editorコンパイル|Unity f3のbatch import終了コード0|
|独立検証ブランチの9クラス結合|実Control＋人工typed key／geometryで152/152 PASS。従来94＋厚さ算術12＋provider結合46|
|元Viewer通常ライブへの統合|未完了。自動採用しない|
|全解像度・全frameでの原作一致|未証明|

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

S生成cameraの正式帰属、自然fade・回転helper、有効描画状態の自然供給、全画素対応は未確定です。最新の原作F採取runの停止判定は `PendingManualRecovery` のまま保持します。ゲーム通常終了後の復元と実行中hookの解除証明は別です。このFork作業で原作ゲームや採取環境は操作していません。

残る問題は [KNOWN_ISSUES.md](KNOWN_ISSUES.md)、APIの使用法は [設計文書](Assets/Scripts/OriginalRendering/README.md) を参照してください。

## 独立検証ブランチでの追加結果

この節は上記35チェックとは別の検証です。描画9クラスと2shaderの実装は `781ef89` から変更していません。

- PR #234全体ではなく、その最初のコンパイル修正 `fbaaa925ab54c9f1f472a8ee7428b7e6ebc7fbff` を出典付きで適用しました。
- 本家ソースの不正なUTF-8文字列を修正し、既存GBKソース8ファイルを可逆にUTF-8へ変換しました。数式や制御処理の変更ではありません。
- sample sceneを含む旧file-browser assetは保持しました。旧assemblyの自動参照だけを無効にし、通常のViewer assemblyは現行file-browserを参照します。
- scene、prefab、ProjectSettings、Packagesの保存差分はありません。
- 全managed／Editor assemblyをUnity 2022.3.62f3でコンパイルし、batch import終了コード0を確認しました。
- 実 `LiveTimelineControl.AlterLateUpdate` と4クラスの結合をD3D11で検証し、**42/42チェックPASS**。Built-in／URP各7更新、provider計16回です。
- key選択と使用ratio、immutable snapshot、k/j独立、MultiCameraNo、camera交換、購読handlerの例外分離、登録所有権、Unknown／他pass時の出力保持、解除後の停止、source buffer保持、所有RT解放を確認しました。

人工geometryと明示済み描画値によるEditor検証です。実楽曲の自然な再生、原作camera帰属、native物理、全frame一致を証明しません。検証環境ではnative物理DLLが利用できず、復元や保護設定の変更、native物理関数の呼出しは行っていません。

実行方法と基盤修正の境界は [INTEGRATION.md](Tools/OriginalLiveRendering/INTEGRATION.md) を参照してください。以前の171チェック、35チェック、原作実測照合は独立した成果として保持します。本家へのPRは作成していません。

## 自然ライブへ向けた静的接続確認

[既存ロード経路の監査](Tools/OriginalLiveRendering/NATURAL_LIVE_PATH.md) を追加しました。キャラクタ構築時のnative物理初期化は既存の物理OFFでは省略されず、評価済み描画値の自然providerも未接続です。また、bridge通知はDirectorのmain camera更新より前なので、下地cameraの選択方法には同一updateの切替を考慮する必要があります。

これはソース監査の結果です。追加run・追加テスト・実装変更はなく、自然ライブ受入へ格上げしません。原作ゲームと隔離DLLは操作していません。

## 同一updateの下地camera選択をGPU検証

上記の静的監査後、同じ検証ブランチのUnity 2022.3.62f3／D3D11で **94/94チェックPASS**。従来の結合42件を再実行し、camera選択の52件を追加しました。Built-in／URP14それぞれ、従来7更新とcamera選択7更新です。

- 実 `Director.InitializeTimeline` によるcamera登録と切替イベント購読を使用しました。DirectorのhandlerやControlをstubへ置換していません。
- 実 `AlterLateUpdate` 内の切替通知からControlのcameraを選び、同じ呼出しで下地のGPU画像が切替先の色になることを確認しました。Control index 0／1／2と明示free cameraの4ケースを両pipelineで確認しています。
- 切替時、bridge内の `MainCameraTransform` は前のcameraを指し、その後に実 `UpdateMainCamera` を呼ぶと切替先を指しました。静的監査の順序を人工fixtureで確認できました。
- camera未通知、範囲外index、free camera未指定では、既存の `MissingSelectedCamera` と出力保持を確認しました。既存ライブへ新しい拒否条件を加えたものではありません。
- 追加はEditor検証と文書だけです。描画9クラス・2shader、Director、Control、通知順序、scene、ProjectSettings、Packagesは変更していません。

試験の選択closureは、通知されたindexと既存Control参照を使う明示登録の利用例です。通常ライブへ自動接続する実装ではありません。実Directorの通常 `Update` 全体や自然楽曲、character animation、post effect、native物理の受入も未実施です。この人工geometryの選択試験では、後段への新しい描画通知を必要としませんでした。

詳細は [結合検証](Tools/OriginalLiveRendering/INTEGRATION.md) と [camera選択の結果](Tools/OriginalLiveRendering/NATURAL_LIVE_PATH.md#同一control更新でのcamera選択検証) を参照してください。過去の原作実測、35／171チェック、Unknownと判定flagは維持します。

## cueから描画値への追加接続

自然providerの不足を一括で推測せず、保存命令で確認できた有効thicknessの分岐を既存producer mathとresolverへ接続しました。選択済みcueのkindとraw thicknessを使用し、固定値・下限・必要なreceiver分岐は明示入力です。評価済みsnapshotとproviderの評価済み値が優先され、既存の `Inputs` constructorでは追加計算を有効にしません。

Unity 2022.3.62f3／D3D11で **152/152 PASS**。従来94件を再実行し、厚さ算術12件と実Controlからのprovider結合46件を追加しました。Built-in／URP14で各16回の追加更新、provider計64回です。

- 選択・補間済みthickness、更新後のkey、明示下限、固定値側、明示receiver分岐、評価済み値優先の各ケースを描画しました。
- 両pipelineの各6ケースで、64×32出力のRGBA全画素が従来の評価済み入力経路と一致しました。raw値と有効値を別々に保持しています。
- shader角度も既存算術へ選択済みrotation sourceと人工の明示依存を渡しました。未取得の実角度定数を同定した結果ではありません。fadeとmappingも明示的な人工入力です。
- kind 0の時間依存分岐、下限、fade、角度依存が未取得の場合は、該当Unresolvedと出力保持を確認しました。
- scalar比較は別途、実SSE MAXSSとの人工81組でbit一致しました。GPUチェック数や原作実測数へ合算しません。

変更した実装は `OriginalCompositeProducerMath` と `OriginalLiveDrawInputResolver` の追加経路だけです。renderer、coverage、shader、adapter、bridge、camera producer、registration、Director、Control、通常ライブの登録と通知順序は保持しています。成功済みfeatureブランチと研究Viewerも無変更です。

これは確認済み算術を自然cueの通知先へ結ぶ段階の **TestOnly** 成果です。固定値・下限の実定数、kind 0の実状態、自然fadeと回転helper、TextureMappingの自然供給、native物理を含む自然ライブは未確定・未検証です。原作の追加採取は行っていません。

## thickness実定数の保存資料確認

`b456e57` 後の文書限定調査です。[定数の根拠と不足範囲](Tools/OriginalLiveRendering/THICKNESS_CONSTANT_EVIDENCE.md) を記録しました。参照命令から2個のfloat32を特定し、関連する保存記録10件を照合しましたが、確認範囲には対象値のbytesが収録されていません。有効thicknessの観測結果だけから固定値／下限値を一意に決めることもできません。

実値はUnknownのまま、既存の明示入力と152/152の過去受入を保持します。次に不足する値自体は各4bytes、合計8bytesです。実装、試験、原作run、開始条件、配備には変更がありません。今回の確認を新しいGPU受入や自然provider完成として扱いません。

## 取得した未確定producer依存の接続

固定厚さ・下限、回転定数、kind別base、duration倍率とfloor helperの参照先を限定実機採取で解決し、既存producer math／resolverへ接続しました。値・private captureは公開コードに埋め込まず、immutableな `ConfirmedProfile` を明示供給します。current keyのfade入力をSelectionから運び、既存評価値の優先と未取得clock等のUnknownを保ちます。

実値profileによる実Control→GPU結合は **175/175 PASS**。従来152＋追加23で、Built-in／URP14の全RGBAが従来の評価済み入力経路と一致しました。原作側も限定採取の正常停止・通常終了・復元を確認しています。過去のPending判定は変更しません。

これは従来の明示登録経路へproducerを接続した成果です。通常ライブの自動置換、自然楽曲通し、native物理、kind 0の動的receiver状態、Viewer型にないsecondary source、TextureMappingの自然供給、S生成cameraの帰属、全frame一致は別の残件です。`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown` を維持します。
