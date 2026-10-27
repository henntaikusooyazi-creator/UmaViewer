# Song cueからwave・PCM先頭への限定対応

## 同期時刻と補助値の追加観測

先行監査で確認したSong時刻関数の、補助値を取得する自然呼出し境界を限定採取しました。同期時刻を保持するレジスタと、補助値の戻り値を別々に保存し、整数幅・符号拡張を区別しています。補助値のhelperには、下位のplayer参照がない場合の分岐もあります。helperの戻り値を保存したことから、全件で下位native parameter APIが呼ばれたとは推定しません。

直接callerだけでなく親の戻り先、同じtyped AudioManager、Songのコピー引数、実playback識別値も照合しました。固定の1 frame補正ではなく、先行getterの完了、同期値・補助値の取得、次の連続getterという3境界を要求しています。条件を満たす保存遷移では、選択分岐とfloat32換算を再計算した値が次のcached getterとbit一致しました。非連続区間は保留し、同じframeの先行getterへ付け替えていません。

親関数の戻り値は今回の2入力から導出した値です。以前の直接source-return観測と、今回の同期値／補助値観測を同じrunの記録として混ぜていません。観測範囲では、補助値が同期時刻を正の値で置換する例はありませんでした。未観測の補助値、cache分岐、音声同期timerの作成設定を補完しません。

既存slotを1枠置き換え、hook数・source行数・読取り量・DLL上限を維持しました。追加のゲーム関数を手動で呼び出していません。実ASM wrapperを通すnative閉鎖試験24件と、実serializerに対応するPython reader試験49件がPASSしました。reader単体のstatement／branchは100%で、collectorや解析CLI全体のcoverageではありません。

最初の起動はDMMトークン期限切れでARM前に終了しました。別runの採取結果も、外側のmetadata監査条件によるPendingを保持します。hook解除・remaining 0・drain・code pin照合と、通常終了後の所有配備復元は別に確認しました。保存観測の再評価を、`StopCompleted`や実行時保持handle復旧へ昇格しません。

今回も実ロードACB／AWBの内容hashや、原作が出力したPCMのsample位置は取得していません。同期時刻が0だった観測、decoderの論理先頭、初期無音は別の証拠です。それらを組み合わせてViewerのPCM epochが成立したとは扱わず、時計や音声offsetのruntime変更も行っていません。

## 先行監査で確認した範囲

以前の音源監査は、既存loaderが選んだ外部AWB内のwaveからcue名を逆引きした結果でした。今回は保存済みSong観測のCueIdを起点に、既存のACB readerを使って参照を順方向へ辿りました。名称が似たwaveを選ぶ処理にはしていません。

対象のcueは、Sequence、Track、TrackEvent、Synth、Waveformを経由し、単一のwaveへ到達しました。CueIdとtableの行番号は別に照合しています。元のSong観測はPendingの保存証拠であり、新しい原作runや正常停止の観測を追加したものではありません。

Coreの既存cue resolverとそのreader sourceも、変更せず私有hostへ参照し、同じCueId・wave ID・cue名を返すことを照合しました。これは同一資産の別実装によるTestOnlyの再評価であり、新たな原作観測やCoreのruntime契約の承認には数えません。DBやSQLiteを開く経路は含めていません。

選択資産では、memory側の先読みデータと外部stream側の両方が参照されていました。次の照合を独立して実施しています。

- ACBが宣言する外部AWBの内容hashと、既存Viewer loaderが選んだファイルのhashが一致。
- ACB内の外部AWB headerと、選択ファイルのheaderが一致。
- memory側とstream側の参照wave IDが一致し、各資産内で対象waveが一意。
- 先読みHCA全体が、外部waveの先頭部分とbyte一致。
- 両者の既存decoderが返したformat・論理sample数・delay情報が一致し、論理位置0からの限定PCM読出しも一致。

PCM比較区間は無音でした。その一致を単独の音源識別根拠にはしません。宣言hash・header・符号化データの先頭一致を併用し、比較区間の後や原作の出力PCMへは拡張していません。資産名、採取値、raw table、音声本体、鍵、native layoutは公開差分に含めません。

## Viewerの論理位置

既存の `HcaWaveStream` はconstructorでdecoder delayを含めた内部位置を設定し、外部へはdelayを除いた論理byte位置を返します。既存loaderのAudioClip位置callbackは、sample位置をchannel数・sample幅に応じたbyte位置へ渡します。今回、先読みと外部waveについてこの既存readerの論理先頭を確認しました。

readerの位置や先読み済みbyte数を、現在の音声出力位置や `AudioSource.time` と同一視しません。今回の比較から新しい時刻offsetや固定frame補正を追加していません。

## Song時刻APIの限定静的対応

既に実戻り値を保存したSong時刻関数から、直接calleeとimport指定を同一buildの保存命令で確認しました。確認した指定名は `criAtomExPlayback_GetTimeSyncedWithAudio` です。別のBGM経路で確認していたGetTimeを、この経路へ流用していません。ライブラリ名とentry名は、実際のpointer／長さ指定と終端を照合し、ASCIIとUTF16を区別しました。

同じ関数には、同期時刻が0のときに補助parameter値を検査する分岐があります。import名の静的対応だけでは、その瞬間のcache分岐、native戻り値、補助値、再生sample位置を実観測したことにはなりません。

現行のCRI公式資料では、GetTimeは経過時間、GetTimeSyncedWithAudioは音声進行に同期する時刻として区別されます。また、再生sample数の取得には別APIがあり、精度はplatformに依存します。これは現行仕様の確認であり、原作buildのSDK versionや実際のtimer設定を証明するものではありません。[CRI公式API資料](https://game.criware.jp/manual/unity_plugin_en/latest/contents/structCriWare_1_1CriAtomExPlayback.html)

## 未確定の境界

| 対象 | 判定 |
| --- | --- |
| 選択ACBのCueIdから参照waveへの順方向対応 | 今回確認 |
| ACBの宣言hashとViewer選択外部AWB | 今回一致 |
| 先読みと外部HCAの先頭・reader論理先頭 | 今回一致 |
| 原作実行時にロードしたACB／waveの資産hash | 未取得 |
| Sequenceのsetup commandが時刻へ与える意味 | 未資格化のまま保存 |
| Songの同期API名 | 呼出し経路上の限定静的確認 |
| 同じSongの同期時刻・補助helper戻り値とcached getter | 追加runの保存境界で限定一致、Pending保持 |
| 原作の実再生sample位置 | 未取得 |
| 原作PCM位置とViewer `AudioSource.time` のepoch | 未成立 |
| 原作とViewerの同一frame最終RGB | 未実施 |

## 先行監査の検証・保全

既存C# reader／decoderを参照する私有の限定toolと、人工入力による順方向route・import文字列の45件のPython試験を使用しました。対象2 Python moduleのstatement／branchは100%です。全ACB形式、製品parser、native collector、解析CLI全体のcoverageではありません。曖昧なcue、欠落参照、循環、複数の再生event、未対応profile、宣言hashやheaderの不一致を拒否し、不足値をゼロ補完しません。

監査初回は不要な文字列列まで読み、既存readerの空文字列処理で失敗しました。製品parserは変更せず、必要な列へ限定し、監査toolの例外を通常の失敗終了へ変更しました。失敗logを保持し、生成toolとそのerror reporterだけをPID／親PID／birthで照合して終了を確認しました。import名でも、ライブラリ名をASCIIと仮定した拒否を保持し、長さ・終端と実encodingを確認する判定へ修正しました。

先行監査では原本を読取のみで扱い、hashを前後照合しました。その監査の追加原作run、Unity再生、GPU readbackは0件です。今回の追加採取でも公開変更は文書のみで、既存498件・142秒Viewer再生・過去のRGB replayは再実行していません。通常UI、時計、renderer、成功ブランチ、既存DLLの未stage状態を維持します。

次の有限対象は、今回の実playback識別値に属する実ロード資産と、実再生sample位置です。同期時刻の整数値をsample位置へ換算して補完せず、資産load／playback bindingとsample取得の実行境界を別々に資格化します。今のcollectorを同条件で繰り返すだけでは、この不足を埋めたことにはなりません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## 再生sample取得境界の追加調査

同じSongの型付きplayback fieldから辿った型について、同期時刻と再生sample数のmanaged wrapper／native import wrapperを限定採取しました。4 methodの先頭命令は同一buildの保存命令と一致し、sample取得側ではimport文字列のpointer・長さ・encoding・終端と、引数からnative呼出し、戻り値のbool変換までを確認しました。子型の行に付くimage名はroot lookupのcontextであり、子型のdeclaring assemblyの証明には使用しません。

`GetNumPlayedSamples` のnative exportは存在します。しかし、同期時刻の関数と同じ内部値を返す処理ではありません。選択したsample取得経路には、出力callbackの後に前回sample値を書き換え、条件に応じてwrap履歴を進める命令があります。このため、手動の問い合わせを原作stateに影響しない観測として追加していません。

追加runの結果は次の範囲に限ります。

| 対象 | 今回の判定 |
| --- | --- |
| 型付きfieldからsample wrapperへの対応 | 保存metadataと命令prefixで限定成立 |
| native exportの現在pointer・命令 | hook準備時の資格化成立 |
| 対象exportの自然呼出し | 観測対象区間で0件 |
| 再生sample数・sample rateの実戻り値 | 未取得 |
| 基底managerの資産load候補 | 限定名selectorで0件。全methodの不存在を意味しない |
| 原作実ロードACB／AWBの内容identity | 未成立 |
| 原作PCMとViewer音声時計のepoch | 未成立 |

既存1枠の置換で、hook上限・source graph・読取上限を増やしていません。DLLは既存上限内です。native専用23ケース、metadata選択53件、reader72件を検証しました。23ケースは通常fixtureとproduction同一定義のfixtureで同じケースを実施しており、独立46件とは数えません。readerは無呼出し、非対象playback、失敗戻り値、欠落出力、不正値、上限超過を分離します。対象2 moduleのstatement／branchは100%ですが、実reader orchestrationやnative collector全体のcoverageではありません。

採取runの外側は `PendingManualRecovery / observation_flags`、保存bodyの `complete/pins_ok=false`、metadata slot不変条件の不成立を保持します。28 hook解除、remaining 0、drain、code pin照合、読取error／limitなしは別の保存証拠です。通常WM_CLOSE後にgame不在と所有配備復元を確認しており、保持handleによる実行時復旧成功やStopCompletedへ昇格しません。

次の対象は、同一buildで限定したnative playback識別値のlookupから実資源と出力callbackを結ぶ境界です。lookupには識別値のpool選択・世代照合があるため、単なるpointer流用や無条件のアドレス計算を採用しません。資産load側も、今回の空selectorを根拠に不存在とはせず、正式なmethod／native資源bindingを別に限定します。sample exportの無呼出しを繰り返すrunや、同期ミリ秒からのsample位置の補完には進みません。

公開runtimeは変更していません。既存498件、142秒Viewer再生、過去のRGB replayは今回再実行せず、今回の受入件数へ加算しません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## Song資源と内部sample照会callbackの追加調査

先行runで保存した実native playback識別値を起点に、同一buildのlookupと資源取得命令を限定確認しました。pool・index・世代照合、bounded list、現在のinterface／context／callback pointerを読み、単なるIDからpointerへの換算にはしていません。保存8件すべてで同じSongの前後資源identityを確認しました。取得した現在callbackのprefixは原作ファイルと一致し、そのthunkから直接calleeへ対象を絞っています。この段階だけではcallbackの自然実行を主張しません。

続く独立runでは、対象calleeを含む限定11命令範囲を実moduleへ照合してからhookを準備しました。追加したhookはなく、既存1枠の置換です。元のcallbackを通常通り1回実行し、その呼出しが書いたsample数とrateの出力を別々に保存しました。戻り値carrierはopaqueのままで、公開SDK queryの成功boolには読み替えません。SDK／ゲーム関数を採取側から手動呼出ししていません。

| 対象 | 今回の判定 |
| --- | --- |
| Song識別値から実資源・callback pointer | 先行runの選択8件で前後identity成立 |
| 内部callbackの自然呼出し出力 | 後続runで16件保存 |
| 同じSong・現在contextと前後資源を満たす出力 | 選択8/8件で成立 |
| その他8件 | fresh資源snapshot対象外として保留 |
| sample数とcontext counter | 選択8件で前後snapshotの範囲内 |
| rateとcontext | 選択8件で両snapshotに一致 |
| callback時点の実Unity frame | 未取得。別スレッドのため保持owner frameと区別 |
| 公開SDK exportの実戻り値 | 今回未観測 |
| PCM出力buffer・実スピーカー位置・Viewer epoch | 未成立 |

sample出力の直接readerとrate取得は限定命令で説明できます。一方、counterを書き進める上流処理、decoderの論理先頭との関係、出力buffering、開始設定は閉じていません。開始側のrateには変化があり、全記録を一定rateで秒へ換算したり、HCA delay情報を未確認offsetとして加減したりしません。前回exportの観測0件はそのまま保持し、今回内部callbackが自然に動いたこととの違いを残します。

最終native callback fixture26件は引数・出力・一度だけのoriginal実行、環境保存、別threadでUnity getterを呼ばない経路、hook復元・上限を確認しました。通常fixtureとproduction同一定義のfixtureは独立52件と数えません。資源／命令資格化fixture81件、Python reader95件もPASSし、対象3 reader moduleのstatement／branchは100%です。先行runの68件／53件は別sourceに対する検証として保持し、最終件数へ合算しません。

両runの外側は `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を維持します。28 hook解除・remaining 0・drain・code pin照合・読取error／上限到達なしと、通常WM_CLOSE後のgame不在・所有配備復元は別に確認しました。StopCompleted、実行中の保持handle復旧成功、過去Pendingの解消には昇格しません。

最初の閉鎖GPU試験でdisable時のthread集合不安定を検出したため、その失敗を保持し、同じ実行ファイル・判定条件による独立再検証と以後のfocused試験を記録しています。追加callback版の最終native試験も通過しています。私有wireの固定長配置・読取上限を維持し、原作バイナリ／音声本体／鍵／native layout／生のpointer・値を公開していません。

次の有限対象は、今回の実contextでsample counterを更新する処理と、同じ再生lifetimeの論理sample位置です。内部query出力の取得を音声出力callback取得やPCM epoch確定へ拡張せず、原作とViewerの同一frame RGBも未成立のまま保持します。通常runtimeは変更せず、既存498件・142秒Viewer再生・RGB replayも今回再実行していません。

## buffer消費とcounter writerの追加実測

保存された現在interfaceの関数参照から、context生成・rate設定・resetと、buffer消費の限定命令へ追跡しました。選択した連続範囲を棚卸しし、同じcounterへ書く複数の処理を分離しています。全module・全writerの閉包や、実際のconstructor実行を証明するものではありません。

消費関数はchannelごとの4byte単位でbufferを進め、累積数を更新した後、次の関係を照会用counterへ書き込みます。

`counter = max(累積消費数 - 保持中の値, 0)`

保存済み前回runの16 snapshotはこの関係を満たしました。rateを渡す別のsetterは変換stateも更新し、counterのresetには別の分岐があります。保持値には設定経路と後続の更新があり、decoder delay、padding、実スピーカー遅延のどれかへ名前だけで読み替えません。

今回の追加runでは、同じSong・contextを使う自然なproducer呼出しを保存しました。calleeの完全な選択命令と直接参照命令を現在moduleへ照合してから、既存1 hook枠を置き換えています。元関数を1回通常実行し、5引数・実戻り値・現在rate・前後context／資源を保存しました。PCMの内容は採取していません。

| 対象 | 今回の判定 |
| --- | --- |
| producerの自然呼出し記録 | 16件 |
| 同じSongと前後資源を満たす選択記録 | 8/8件 |
| 戻り値と累積消費数の増分 | 選択8件すべて一致 |
| 前後の照会用counter算術 | 選択8件すべて一致 |
| sample-rate変換 | 選択8件は単位stepに限定 |
| 残る8記録 | fresh資源snapshot対象外として保留 |
| decoder論理位置・出力buffer・Viewer PCM epoch | 未成立 |

開始側では、消費数が増えても保持値を超えるまでは照会用counterが0となることを実測しました。したがって、同期時刻0、内部counter0、decoder論理位置0は別々の証拠面です。固定rateで割った秒数や、保存されたHCA情報を未確認offsetとして用いる接続は追加していません。

producerはowner getterと別threadで実行されました。保持frameを実Unity frameとして使わず、今回のcounter更新を以前のGPU frameへ結合しません。単位stepの8件を、異なる変換比率・loop・seek・pause・全sourceへ一般化しません。

閉鎖native ABI26ケース、資源／命令資格化89ケース、Python120ケースPASS。通常fixtureとproduction同一定義fixtureの26ケースは独立52件へ合算しません。読取対象3 moduleは185 statements・40 branchesで未通過0。限定PE読取の境界試験も含みますが、解析CLIやnative collector全体のcoverageではありません。5引数のstack転送を検証し、観測側は32bit引数の下位bitを型に沿って保存しています。原作へ渡す引数値の補正はしていません。

外側 `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を保持します。28 hook解除・remaining 0・drain・code pin照合・読取error／上限到達なしと、通常WM_CLOSE後のgame不在・所有配備復元を別に確認しました。StopCompletedや実行時の保持handle復旧成功へ昇格しません。

次の有限対象は、差引かれる保持値の実設定入力と、同じ再生lifetimeのbuffer先頭・decoder論理先頭との対応です。counter更新の再採取を増やすだけではPCM epochが確定しないため、今回限定した設定・変換・消費の境界を起点にします。公開runtimeは変更せず、既存498件・142秒Viewer再生・過去のRGB replayも今回再実行していません。各false／Unknown gateと歴史的Pendingを維持します。

## 保持値の実設定入力とconsumerへの限定lineage

前回限定した保持値設定の上流を、同一buildの保存命令で確認しました。保持値生成にはfloat32乗算が2回あり、現在rate、`held_source_input`、資格化した係数を命令順に評価して整数化します。入力fieldの単位・演出的な意味は確定していません。

追加runでは既存3枠を、buffer consumer、入力setter、保持値書込み後に必ず通る選択calleeへ割り当てました。不要なshader生成採取は外し、全targetの資格化・準備を初回有効化より先に完了させています。hook上限・source graph・既存の読取と保存上限は拡張していません。対象のゲーム／SDK関数を手動で呼び出さず、自然呼出しの元関数を1回実行しています。

| 対象 | 今回の結果 |
| --- | --- |
| 実setter引数とfield書込み | 1件、float32 bit一致 |
| 保持値確定後checkpoint | 1件、現在入力・rateからの限定算術と一致 |
| consumer記録 | 14件 |
| fresh資源を保存した記録 | 8/8件資格化。setter・checkpoint各1件、consumer 6件 |
| 設定入力→保持値→最初の観測consumer | 同じSong・context、前後資源identityと実行順で1本成立 |
| その他8件 | fresh資源snapshot対象外として保留 |
| PCM論理先頭・実スピーカー位置・Viewer epoch | 未成立 |

setterとcheckpointでは、型資格化済みの現在AudioManagerとSongをfield読取だけで参照し、実資源の世代を照合しました。checkpointでは親contextと実callee引数のchildを区別して保存しています。開始処理のUnity frameは不明と明示し、音声threadの保持owner frameも実実行frameへ昇格しません。

観測順は、setterの終了、保持値書込み後checkpoint、累積消費数0から始まる最初の観測consumerです。最初のconsumerで累積消費数が進んでも照会counterは0のままで、後続consumerでは同じ保持値を減算した結果へ進みました。観測した保持値は前後で不変です。ただし、未観測のゼロ消費呼出しや全writerの不存在は主張せず、一般的な再生初期化・非単位resampler・seek／pauseへ拡張しません。

閉鎖試験は既存producer ABI26件、追加setter／checkpoint ABI12件、資源・命令資格化99件、Python152件PASSです。新しい保持値readerと固定長wire readerは131 statements・36 branchesで未通過0。これは対象readerのcoverageであり、解析CLIやcollector全体のcoverageではありません。通常fixtureとproduction同一定義fixtureは同じケースの再評価です。

外側は `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を保持します。28 hook解除、remaining 0、drain、code pin照合、読取error／上限到達なし、通常WM_CLOSE後のgame不在と所有配備復元は別に確認しました。StopCompletedや保持handleによる実行時復旧成功には昇格しません。

次の有限対象は、今回のbuffer consumerへ渡されたbuffer先頭とdecoder論理先頭の対応です。保持値をdelay／padding／speaker latencyと呼び替えず、`held_source_input` の無意味名を維持します。通常runtimeの変更、既存498件・142秒Viewer再生・RGB replayの再実行はありません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## buffer入力headerと生成側の採取境界

consumerの第5引数は出力先のchannel配列でした。実際の消費元はcontext内queueのbufferです。queue投入、pool移動、入力bridgeの限定命令を追い、channelごとの明示copyと、別processorを呼ぶ分岐を分離しました。bufferに保持されるopaque値をdecoder論理位置と命名していません。

追加runでは既存3枠をconsumer・queue投入・入力bridgeへ割り当て、型付きSong、世代照合済み資源、前後のbuffer headerと実callerを限定保存しました。PCM本文と出力波形は採取していません。

| 対象 | 結果 |
| --- | --- |
| 保存記録 | consumer 16件、生成側2役割は採用0件 |
| fresh資源とheaderを保存 | 4件 |
| 単位step・header進行・累積増分を資格化 | 3件 |
| 残るfresh 1件 | 単一buffer内に閉じない消費として保留 |
| 入力bridge→queue投入→consumer | 未成立 |
| decoder論理位置・Viewer PCM epoch | 未成立 |

採取枠は開始側・後半側ともconsumerが短時間で使い切りました。生成側が採用されなかった原因をこれだけに限定はできませんが、次の別版ではconsumerによる枠の占有を防ぎ、生成側を待つ役割別quotaを追加しました。総保存数、fresh資源数、読取、sidecar、hook数の上限は維持しています。bufferの再利用・所有権の移動後は内容を再読せず、未取得・processor不明・非単位stepを成功へ補完しません。

比較器は、再生開始時の累積0を必要とする主張と、観測したbufferから後続consumerへの接続を分離しています。後者が成立しても、再生の最初のbufferやdecoder論理先頭の証明にはなりません。入力bridgeの関数pointerを保持する静的登録表も限定確認しましたが、実callerやdecoder帰属へ昇格していません。

最終の閉鎖native試験は既存producer ABI26件とbuffer／役割枠22件、資源・命令資格化104件、Python134件PASS。buffer比較readerは114 statements・38 branchesで未通過0です。通常fixtureとproduction fixtureの同じケースは独立件数へ加算しません。最初の版のbuffer試験18件・Python133件は別版の履歴であり、最終件数へ加算しません。

再採取の準備後、DMMの自動フォーカスが拒否され、原作接続待ちの既存上限に達しました。この試行はゲーム起動0・ARM0で、原作採取や生成側lineageの成功ではありません。未起動の接続待ちPendingと、採取runの `PendingManualRecovery / observation_flags` を分けて保持しました。採取runは `complete/pins_ok=false` とmetadata slot不変条件の不成立を維持し、28 hook解除・remaining 0・drain・code pin照合・読取error／上限到達なしを別に確認しています。通常WM_CLOSE後のgame不在と、未起動試行のgame不在をそれぞれ確認し、所有配備を復元しました。StopCompletedや保持handleによる実行時復旧成功へ変更しません。

次の有限対象は、役割枠を確保した採取版で同じSongの入力bridge・実queue投入・consumerを結び、実callerからbuffer生成元を限定することです。decoder論理位置は直接の状態・pointer・資源世代・実行順で結び、counter、rate、HCA既知値から逆算しません。公開runtime変更、498件・142秒Viewer再生・RGB replayの再実行はありません。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## 自動操作復旧後のbuffer再採取

DMMツールは入力前のnative前面化要求と有限待機を共通化しました。APIの成功だけでは入力を許可せず、実foreground、HWND／PID／生成時刻、geometryを再照合します。既存のdesktop・integrity・署名・観測期限・単回token・遮蔽確認は維持し、背景への直接入力や補助キー送出、security設定変更は追加していません。ローカルtoolの251件PASSと、背面のDMMを自動前面化してnavigationした2件を確認しました。旧接続は切断状態のまま、新規stdio接続で修正版を使用しています。

この接続からランチャーを操作し、ゲーム起動、今回起動したownerの採取前照合、楽曲選択、再生確認、一度だけのARM、native準備完了後の再生まで進めました。DMM／ゲームへの入力は計7件で、手動前面化は0件です。UI画像は専用private rootの既存容量上限内に保持し、アカウント表示やraw採取値は公開していません。

| 対象 | 今回の結果 |
| --- | --- |
| consumerの保存・資格化 | 同じSongのfresh資源とheaderを持つ2件 |
| queue投入候補の自然呼出し | 観測あり。ただし採用条件を通る記録は0件 |
| 選択した入力bridgeの自然呼出し | 観測0件 |
| 入力bridge→queue投入→consumer | 未成立 |
| decoder論理先頭・Viewer PCM epoch | 未成立 |

役割別quotaによってconsumerの先行占有は抑制されましたが、生成側の結合は成立していません。関数呼出し数はcaller・Song結合などの採用判定より前の値であり、対象Songの独立観測数ではありません。bridgeの観測0件も原作全体での未使用証明ではありません。group判定の拒否数だけから、先行するcaller条件やcontext条件を通過したと推定しません。

次の有限edgeは、今回のSong contextへ入るqueue投入の実callerを資格化し、静的bridge候補がその入力経路に該当するかを確認することです。未知callerを既知bridgeへ読み替えず、実module・call境界・資源世代・順序に沿って扱います。PCM本文の採取やconsumer数の増加だけでepochを補完しません。

同じ最終sourceでproducer ABI26件、buffer／役割枠22件、資源104件、Python134件PASS。対象buffer readerの114 statements・38 branchesは未通過0です。通常／production fixtureや前回の同一定義再実行を独立件数へ加算しません。Core保護13件、原作4ファイル、採取前proofの168 source／検証入力のhash不変を確認しました。

外側は `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false`、metadata slot不変条件の不成立を維持します。28 hook解除、remaining 0、drain、code pin照合、読取error／上限到達なし、通常WM_CLOSE後のgame不在・所有配備復元は独立して確認しています。StopCompletedや保持handleによる実行時復旧成功には変更しません。準備後に未起動で終了した前回試行も、今回の実採取へ置換していません。

公開runtimeは変更せず、今回の公開対象は文書2件です。既存498件・142秒Viewer再生・RGB replayは未再実行です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。
