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
