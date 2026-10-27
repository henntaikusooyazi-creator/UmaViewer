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
