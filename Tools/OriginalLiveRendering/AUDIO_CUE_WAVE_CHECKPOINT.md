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

## 同一Songの実queue callerと観測bufferの消費

前回は固定caller条件を満たすqueue投入の採用が0件でした。今回は旧lineage readerを変更せず、別のcaller観測schemaで、型付きSongの現在識別値と世代照合済みcontextに一致するqueue投入を最大2件保存しました。元関数は1回だけ実行し、PCM本文は取得していません。hook数、source graph、DLL、source保存16件・fresh資源8件、読取とsidecarの上限は維持しています。

| 対象 | 今回の結果 |
| --- | --- |
| 保存記録 | queue投入2件、consumer 4件 |
| 実callerの資格化 | 2/2件。前後の同一Song資源、header、caller命令、call境界が一致 |
| owner／interface table | 2/2件。保存registerと同じSong資源の前後snapshotに対応 |
| interface dispatch | 2 slotが同じqueue投入関数へ到達。保存動的値と同一buildの限定命令を照合 |
| consumerの限定資格化 | 3/4件。残る1件は単一buffer内に閉じない消費として保留 |
| queue→consumerのpointer進行 | 2 buffer・3組の観測対応 |
| 選択した旧入力bridge | 自然観測0件。全体での未使用や上流での不関与は未証明 |
| poolへの生成writer、decoder論理先頭、PCM epoch | 未成立 |

実callerは、前回仮定したbridgeの直接callerとは異なる2関数でした。両者が使うownerは、同じSongのlookupで得られる実資源内のobjectと一致しています。interface tableも動的registerとsnapshotで対応し、そのtableの限定slotとthunkから、今回観測したqueue投入関数へ結びました。新たな同一frameのGPU command生成ownerや音声出力位置を証明したものではありません。

同じbufferに対する複数consumerを独立した生成元観測へ合算しません。確認したのは、queue投入後のnodeと、後続consumerのnode・owner・寸法・残量・channel pointerの進行関係です。全writer閉包、再生の最初のbuffer、decoderの論理sample番号は主張していません。

callerの命令はpoolの指定streamからbufferを取得していました。poolの取り出し・再投入の有限命令も確認しましたが、そのstreamへ実際にbufferを生成して渡したwriterは未観測です。次の有限edgeは、この同じpool／nodeへ投入するwriterとその入力です。古いbridge候補へ無理に当てはめず、実callerで確認したowner・poolの境界から追跡します。counter、rate、HCA既知値によるdecoder原点の逆算は行いません。

閉鎖検証はproducer ABI26件、buffer／caller採用境界30件、資源104件、Python168件PASS。新caller readerの63 statements・24 branchesは未通過0です。旧readerへ新schemaを渡した初回試験は拒否され、形式別fixtureの参照先を分離して再検証しました。旧readerの受入条件を変更して通したものではありません。同じ試験定義・同じ保存観測を別々の独立実測へ加算しません。

今回も初回のDMM自動前面化は拒否されました。この未完了を成功へ変更せず、既存の明示activateで前面化を復旧し、再観測したtokenから操作を続けました。通常ゲーム起動から再生まで7入力、手動前面化0件です。DMM tool sourceの変更や251件の再実行は今回行っていません。

外側 `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を維持します。28 hook解除・remaining 0・drain・code pin照合・読取error／上限到達なし、通常WM_CLOSE後のgame不在と所有配備復元を別に確認しました。Core保護13件・原作4ファイル・proofの170 source／検証入力のhashは不変です。StopCompletedや保持handleによる実行時復旧成功へ変更しません。

公開runtimeは変更せず、文書2件のみを反映しています。既存498件・142秒Viewer再生・RGB replayは今回未再実行です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## 同一Songのpool writerからqueue・consumerへの接続

実queue callerが使用するpoolの指定streamへ、同じSongのbufferを渡すwriterを限定しました。新しいpool観測形式を使い、旧caller／lineage readerの受入条件は変更していません。PCM本文、手動SDK呼出し、原本複製は追加していません。

| 対象 | 今回の結果 |
| --- | --- |
| 先行採取 | writer 2件を資格化。ただし非単位stepかつqueueと別nodeで、writer→queueは0件 |
| 最終採取の保存 | writer 2件・queue 2件・consumer 4件、全8件にfresh資源snapshot |
| 最終採取の資格化 | writer 2/2件、queue 2/2件、consumer 2/4件 |
| pool writer→queue→consumer | 2 bufferそれぞれで1本の順序付き対応 |
| writer callerのowner・出力node | fresh資源、保存register、実call境界と2/2件で対応 |
| 残るconsumer 2件 | 単一buffer内に閉じない消費として保留 |
| 最初の再生buffer・decoder論理位置・Viewer PCM epoch | 未成立 |

先行runの結果から、role quotaだけでは同じnodeを追えないことが分かりました。最終版では単位stepのwriterを最大2件保存し、そのnode・Song識別値・pool・contextが一致するqueueだけを採用します。consumerも、保存queueへ実際に渡された同じnodeだけを対象とします。前後snapshot、実caller、buffer header、pointer進行の資格化は別途維持し、選択されたことだけを成功にしません。総保存16件、fresh資源8件、hook数、読取とsidecarの上限は維持しています。

2つの接続は、各bufferのwriter出力nodeが後続のqueueとconsumerへ渡る対応です。poolの全履歴、未観測writerの不存在、再生先頭のbuffer、decoderのsample番号を証明するものではありません。writerの実callerには入力側の処理があり、直接calleeとinterface dispatchを限定静的確認しましたが、実際の入力interface／table／context／targetとdecoder状態はまだ未採取です。次はこの実ownerに属する入力経路だけを資格化します。counter、rate、HCA既知値から位置を逆算しません。

閉鎖nativeはproducer ABI26件、buffer／選択・ABI52件、資源105件、Python216件PASS。pool readerの77 statements・34 branchesは未通過0です。これは対象readerのcoverageであり、collector全体やdecoder全体の証明ではありません。同じ定義の通常／production fixtureを二重計上しません。collector source graphとDLLサイズ、元関数1回実行・ABI・environment復元を既存上限内で確認しています。

未ARMで既存待機期限へ達した試行は、ARM0のまま別記録に残しました。最終runのUIクライアントでは、要求ファイルが完成する前に読まれる競合を確認し、完成した要求のrename公開へ修正しています。新規stdio接続へ切り替え、同じゲーム・controllerで続行しました。入力期限やowner照合、採取deadlineを緩めていません。手動前面化は0件です。誤ったclose引数のschema拒否も成功へ置換せず、正しいexpected identityを使ったWM_CLOSEとgame不在を確認しました。

2採取runは外側 `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を維持します。28 hook解除、remaining 0、drain、code pin照合、読取error／上限到達なし、通常終了後のgame不在・所有配備復元は別に確認しました。未ARM試行も配備復元済みです。Core保護13件、原作4ファイル、採取前proofのsource／検証入力は各runでhash不変です。StopCompletedや保持handleによる実行時復旧成功へ変更しません。

公開変更は文書2件のみです。private source、採取値、pointer・命令・native layout、UI画像はローカルの再評価可能な証拠として保持し、公開しません。通常runtime、成功ブランチ、DLL未stage削除は維持し、既存498件・142秒Viewer再生・RGB replayは今回未再実行です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## 同一Songのdecoder状態と出力先の限定接続

pool writerの実ownerが保持する入力interface・table・context・targetを、同じSong資源の前後snapshotと2/2件で照合しました。そのtargetの実prefixと同一buildの限定命令を確認しています。これはcurrent targetの資格化であり、そのinterface関数入口を観測した同一call traceとは区別します。先行runの2 bufferについて、入力snapshotから既存writer→queue→consumerへの対応が成立しました。

限定命令から直接到達するdecoder出力producerを、既存queue観測slotへ明示的に置換しました。引数・実戻り値・environmentを保持し、元関数を自然呼出しで1回だけ実行します。PCM本文、手動SDK呼出し、原本複製は追加していません。先行版はskip／末尾側のcallsiteを選んでおり、writerとconsumerは取得できてもdecoder採用は0件でした。この結果を残し、通常分岐のcallsiteを同一buildの命令で資格化し直しました。修正版はそのcallsiteだけを採用し、他のcallsiteを拒否するnative／Python試験も追加しています。

| 対象 | 最終runの結果 |
| --- | --- |
| 保存記録 | decoder 2件、pool writer 2件、consumer 4件 |
| 限定資格化 | decoder 2/2件、writer 2/2件、consumer 2/4件 |
| 現在の入力context→decoder | 同じSong資源・component・interface contextの保持handleと実引数を照合 |
| decoder状態 | 連続2呼出しの状態進行を直接確認。絶対sample番号とは扱わない |
| decoder出力範囲→writer→consumer | 2対応。両者は同じ1 bufferを共有し、独立bufferは1件 |
| writer→consumer | 2 bufferで対応。ただしdecoderとの対応があるのはうち1 buffer |
| 残るconsumer 2件 | 単一buffer内に閉じない消費として保留 |
| 再生先頭・decoder絶対sample原点・Viewer PCM epoch | 未成立 |

decoderでは、実引数のhandleが現在の入力contextに保持される実体と一致することを確認しました。出力channel配列が示す範囲は後続writerのbuffer内に閉じ、両channelのoffsetが一致しています。同じthread、実行順、Song識別値・context・資源世代の前後一致も必要条件としています。decoderのskip、意味未確定のcomponent選択値、単位stepの条件を区別し、counter・rate・既知HCA値から位置を逆算していません。

追補訂正：当初trimと呼んだcomponent選択値の意味付けは未成立でした。同じ数値変位を含む命令はstack引数の参照であり、そのcomponent値と同じものとは証明できません。旧観測値・pin済みsourceを保持して意味未確定へ戻します。実際に取得したdecoderのskip、および出力範囲からwriter／consumerへのpointer対応は別証拠として維持します。

2回のdecoder出力が同じbufferへ入るため、2組の接続を独立した2本の生成lineageとして数えません。queue観測slotはdecoderへ置換しているので、今回decoderからqueue呼出しまで同時にtraceしたという主張もしません。先行runのqueue証拠は別runの限定証拠として保持します。PCM内容、全writer閉包、実スピーカー出力位置、Unity frameとの対応を証明するものではありません。

最終sourceの閉鎖検証はproducer ABI26件、legacy buffer67件、decoder-mode28件、資源107件、Python297件PASS。RuffもPASSし、decoder readerの49 statements・18 branchesは未通過0です。legacy67件をdecoder-mode試験とは記載せず、同じ通常／production fixtureや保存観測の再評価を二重計上しません。既存のhook数、source graph、DLLサイズ、source／fresh資源、読取・sidecarの上限を維持しています。

未ARM試行のtoken期限切れ・再起動、日付変更と重なったowner待機期限、配備前backup不足による拒否、native fixture生成完了前にPython試験を開始した失敗も記録しました。配備前の拒否には所有配備の変更がなく、既存owned backupのhash照合後に同じ生存controllerへ配備を再開しています。依存fixtureの生成完了を待ち、Ruff→関連pytestを直列で再実行しました。再試行では再生操作直後の本編開始を確認し、PID・birth・run IDを混ぜていません。

採取に到達したrunは外側 `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false` とmetadata slot不変条件の不成立を保持します。最終runの28 hook解除・remaining 0・drain・runtime／GPU code pin照合・読取error／上限到達なし、通常WM_CLOSE後のgame process／window不在・所有配備復元は別に成立しました。owner待機期限の試行では初期5 hookだけが有効で、その5件の解除を別に確認しています。いずれもStopCompletedや保持handleによる実行時復旧成功へ変更しません。Core保護13件・原作4ファイル・最終proof191 source／検証入力のhashは不変です。

証拠・source・再現手順は上限付きのdurable manifestへ保存しました。全archive hashを検査し、元の一時ファイル・ゲーム原本へのreadを拒否した状態でも、入力snapshotと最終decoder接続の同じ判定を再現しています。private値・命令・native layout・UI画像は公開していません。

次の有限edgeは、直接観測したdecoder状態と出力範囲を、符号化block位置と明示初期化／範囲設定へ結ぶことです。phaseの進行を絶対sample原点へ読み替えず、再生最初のbufferやPCM epochはUnknownのまま扱います。公開変更は文書2件のみで、通常runtime、成功ブランチ、DLL未stage削除を維持します。既存498件・142秒Viewer再生・RGB replayは今回未再実行です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## decoder初期化から明示rangeと符号化block入力への限定接続

前回の日付変更と重なった試行を置換せず、新run ID・PID・birth・controllerで再試行しました。再生確認画面を確認し、再生前のARMとhook準備のreceiptが成立してから本編を開始しています。手動SDK呼出し、PCM本文、原本コピーは追加していません。

| 対象 | 最終runの結果 |
| --- | --- |
| strict readerによる採用 | header初期化1/1件・range1/1件・block4/4件。拒否0件 |
| 同じ実体への対応 | Song・資源世代・component／interface／入力context・decoder handle・thread、前後snapshotが一致 |
| 初期化入力 | headerの実6成分が現在decoderのmetadataへ一致 |
| range入力 | 実2引数と実decoder状態への書込みが一致 |
| 順序付き対応 | 初期化→range→4 block入力。1本のlifetimeであり、独立した4原点ではない |
| rangeの限定再計算 | 保存したloop末尾値と実caller contextを要求し、非正loop長の観測入力から実引数を再現 |
| header相対block位置 | 最初の1件のみ保存header入力span内で候補を対応。残る3件はUnknown |
| 絶対sample原点・元資産位置・Viewer PCM epoch | 未成立 |

今回のrange再計算は、事前モデルの条件を広げて旧判定を書き換えたものではありません。旧モデルは条件外のまま保持しています。strict readerを通った実引数・実書込みの対応と、保存済みloop末尾値を加えた限定CPU再評価を別に記録しました。positive loop経路や一般的な初期化規則へ拡張せず、counter・rateからsample位置を逆算していません。

最初のblockは保存されたheader入力span内でheader直後と整合しました。ただし、pointer範囲の対応だけでは符号化bufferのwriter・lifetime・版や、元資産の絶対block番号は確定しません。4件はblock入力の自然呼出しであり、4個の独立したPCM出力bufferではありません。前回のdecoder出力→pool writer→consumerは別runの限定証拠として保持し、今回同時に閉じたlineageへ混ぜません。音声出力位置、音声と描画の同一frame、Viewer PCM epochも未証明です。

先行run54は初期化前の対象外blockが候補上限を使い切り、採用0件でした。同一handleの初期化とrange確定後だけblockを候補へ進める条件に修正し、上限と資源照合を維持しています。run55は6記録を保存したものの、4記録のalignment paddingにより旧readerが拒否しました。生記録をゼロ補正せず拒否結果を保持し、origin記録の全byteを明示初期化する修正とfixtureを追加して再採取しました。旧padding検査、wire、hook数・DLLサイズ・source graph・候補／保存／読取の上限は維持しています。

解析用cloneの相対run名残留とselected JSON不足も検出しました。新run IDを明示検証し、配備前に必要な入力を確認する手順へ修正しています。別runの保存値を現在の結果へ混ぜていません。失敗・修正・再発防止は私有checkpointへ残しました。

最終sourceの閉鎖検証はorigin-mode56件・資源107件・事前Python342件PASS。origin readerの115 statements・40 branchesは未通過0です。保存後の限定lineage／range再評価23件もPASSし、58 statements・22 branchesは未通過0です。Ruff PASS。legacy producer ABI26件・buffer67件・decoder-mode28件は独立したカテゴリとして保持し、同じfixtureの通常／production再評価を二重計上しません。

3 runの外側 `PendingManualRecovery / observation_flags`、bodyの `complete/pins_ok=false`、metadata slot不変条件の不成立は維持します。28 hook解除・remaining 0・drain・runtime／GPU code pin照合、read error／上限到達なし、通常WM_CLOSE後のprocess／window不在・所有配備復元は独立確認しました。StopCompletedや保持handleによる実行時復旧成功へ変更しません。Core13件・原作4ファイル・最終preflight201 source／検証入力はhash不変です。

source・証拠・再現手順は上限付きdurable manifestへ保存し、全archive hashを確認しました。元temp・ゲーム原本へのreadを拒否した再評価で、先行失敗の拒否結果と最終6記録・順序付き対応・限定range再計算を再現しています。private値・命令・layout・UI画像は公開していません。

次の有限edgeは、最初の符号化入力bufferのwriter／版と元資産範囲への直接対応です。header相対の候補を絶対sample原点へ読み替えません。公開は2文書のみで、通常runtime・成功ブランチ・DLL未stage削除を維持し、既存498件・142秒Viewer再生・RGB比較は未再実行です。`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。

## 本編の符号化入力packet・pool・decoderの同一run接続

本編音源の現在Song資源を、自然getterの実receiver、現在の型と音源識別、前後資源snapshotで限定資格化しました。getterのreceiver資格化と、失敗した時計・値joinを区別しています。previewの限定観測を本編へ流用せず、過去の本編初期化・range記録も別証拠として保持しました。

| 対象 | 最終runの判定 |
| --- | --- |
| 符号化入力の書込み | 2/2件採用 |
| 現在input poolからの取得 | 2/2件採用。先行1件は書込み未追跡として保持 |
| decoder block入力 | 4/4件採用 |
| 実入力packet metadataと書込みnode | 2/2件対応 |
| 書込み→追跡済み取得→block入力 | 同じ1 bufferの最初のblock、1組のみ |
| 共有読取り上限 | 未到達。設定済み記録の完了時に採取windowを閉鎖 |
| 元資産の絶対位置・decoder絶対sample原点・Viewer PCM epoch | 未成立 |

入力packetは、実書込みcallerの保持するsource側実体に対応します。保存済み完全関数と小さなcalleeを限定確認し、同じ書込みの前後でpacketのdata・長さ・offset候補・node参照を読み取りました。data範囲と書込みnodeの関係が一致しています。ただしpacket内offsetをファイルoffsetと命名せず、元資産のどの絶対blockから生成されたかはUnknownです。PCM・符号化本文、SDK手動呼出し、原作媒体コピーは追加していません。

4 blockは同じ1本の完全生成lineageを4回独立観測したものではありません。strict readerが同じ取得snapshotへ直接結べたのは最初のblockだけです。もう一つの書込みpacketと先行未追跡取得も、この接続数へ合算しません。以前のdecoder出力→consumer→counterや初期化→rangeは別runの限定証拠です。

先行run57～62では、対象外資源・previewやmanager取得不足を区別しました。run63～64では本編記録を保存しましたが、終了時の共有読取り上限到達によりreaderは拒否しています。その結果を緩めず、新版では対象外callbackを読取り前に除外し、追跡済み取得の保存枠を確保しました。必要な8記録が完了したらcollectorの追加時計・parameter採取を停止し、最終runで上限内の採取を成立させています。ゲームの時計・再生状態を変更する処理ではありません。

DLLサイズ上限超過は配備前に拒否し、重複snapshotと専用版の不要な出力を整理して既存上限内へ戻しました。閉鎖GPU試験の一時的な解除失敗と作業directoryの誤りによる失敗を保持し、正しい専用directoryの単独再試験と一式検証を通しています。所有backup不足、UI画像上限、確認から再生までの期限、通常終了後の不在確認などの途中失敗・修正は私有checkpointへ記録しました。read-onlyのdispatch参照調査は実読取り不成立として残し、実targetやbuffer版の成功へ昇格していません。

最終sourceの閉鎖検証はencoded74件・資源107件・Python438件PASS。reader117 statements・32 branchesは未通過0、Ruff PASSです。legacy producer ABI26件・buffer67件・decoder28件・origin56件は別カテゴリとし、通常／productionの同一fixture再評価や過去観測を二重計上しません。hook数、source graph、DLL、候補・保存・resource／buffer／input・sidecar・共有読取りの既存上限は維持しました。今回明示したpacket metadataも有限2件に限定しています。

最終runも外側 `PendingManualRecovery / observation_flags`、body `complete/pins_ok=false` と既存metadata slot不変条件の不成立を保持します。28 hook解除・remaining 0・drain・runtime／GPU code pin照合と、通常WM_CLOSE後のprocess／window不在・所有配備復元は別に確認しました。Core13件・原作4ファイル・最終proof222 source／検証入力のhashは不変です。StopCompletedや保持handleによる実行時復旧成功へ変更しません。

source・証拠・再現手順を上限付きdurable manifestに保存し、全archive hashを検査しました。元temp・ゲーム原本へのreadを拒否した保存後再評価で、旧runの拒否と最終8件採用・packet対応2件・限定接続1件を再現しています。private値・命令・layout・UI画像は公開していません。

次の有限edgeは、この実packetをsource queueへ渡したproducer、その入力read resourceと絶対位置の結合です。counter・rate・既知HCA値から補完しません。公開は2文書のみで、通常runtime・成功ブランチ・DLL未stage削除を維持し、既存498件・142秒Viewer再生・RGB比較は今回未再実行です。`OriginalVerified=false / runtime_contract=false / SourceToViewerClockContract=false / Phase257cause=Unknown` と歴史的Pendingを維持します。
