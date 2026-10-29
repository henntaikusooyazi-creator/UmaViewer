# Viewer streaming音声の限定観測

`StreamingAudioCapture`は、既存のstream callbackを明示登録したときだけ記録します。元音源を再解析せず、従来のsample providerとstream Positionを利用します。通常UIへの自動登録はありません。

## 全ブロックと読取り段階の保存監査

`pcm_source_domain.py`は`round_float32(sample / rate)`の厳密な逆像を、隣接float32の中点・nearest-evenの端点と有理数算術で求めます。正当化したclip範囲と実観測sample集合だけを対象にし、最近傍sample、一定buffer刻み、float秒へのepsilonで一致を作りません。この数式がUnityのすべての再生状態を表すという保証でもありません。source値が同じでも、owner・DSP/filter・GPU frameが一致するとは認定しません。

明示staged再生では`pcm-source.private.json`へ実producer世代を保存し、`pcm-clock.private.json`のsource-read段階とconsume段階、owner・selected／submissionを照合します。別のUnity frameに属する読取りを同じframeのAPI読取りとして記録しません。最終自然試験の3,258世代で実読取り→一度の消費→owner／submissionが対応しました。これはViewer内部のlifetime保証に限定し、原作run89の自然tuple一致0件と原作同一frame全面RGB未成立を維持します。

今回の公開Pythonは既存60＋追加19の79件PASS、3 moduleの149 statements・74 branchesは未通過0です。原作数値や媒体・private observerは含まず、公開fixtureは人工値です。

`Validation/PcmPhase/pcm_content_match.py`は、明示したchannel数・coverageを持つPCM float bytesから、非無音の完全一致位置を探します。探索範囲内で一致候補が複数なら拒否し、未採取範囲やsample境界外の一致を採用しません。一意性は入力した選択範囲内のものです。媒体の解析・追加decode・原作プロセスへの接続は行いません。

`pcm_phase_audit.py`は保存済みの全filterブロックと、同じcapture lifetimeのsource/clip・現在sample・DSP読取・owner/submission記録を監査する補助関数です。sampleはchannelあたり、PCMはinterleaved float32 little endianです。providerは4 MiB以下、filter保存全体も4 MiB以下、各ブロックは128〜2,048 sample、最大256ブロック、選択区間は最大4秒です。省略した末尾、無音、曖昧な一致、違うsource/clip、単位違い、時刻逆順を拒否します。

- `match_blocks(window, provider, coverage, filtered)`は、実採取された全ブロックのbyte一致とDSP sample座標を返します。`firstFloat`はfloat要素番号、`capturedFrames`と`bufferFrames`は等しいことを要求します。
- `read_phase(block, owner, rate)`は同じ論理sample範囲に属する現在sampleについて、filterコピーとmain読取りの先後を返します。入口timestampが明示されている場合だけcallback入口の先後も返します。コピー開始を入口へ読み替えません。source/clipの数値IDは同一capture内だけで有効で、呼出し側が資産identity・lifetime・owner→submissionの資格を別に維持します。
- `contain_interval(matches, begin, end)`は半開区間の全体包含を判定します。包含成功でも`point_owner_join=false`、`original_same_frame_RGB_admitted=false`です。別runの同一sample区間を同時刻や同一frameと認定しません。

DSP秒から整数sampleへの変換は、実値が整数sampleのfloat表現誤差内にある場合に限ります。観測された差を時計補正へ適用せず、範囲外の点や欠落したownerを補間しません。fixtureは人工PCMのみで、原作の採取値・音声本文・shaderを含みません。

検証例（既存のuv管理環境を使用し、coverage出力先はrepo外へ指定）：

```powershell
<python> -m ruff check Tools/OriginalLiveRendering/Validation/PcmPhase
<python> -m pytest -q -p no:cacheprovider --cov=pcm_phase_audit --cov=pcm_content_match --cov-branch <selected tests>
```

pytestは当該folderを作業先とし、`COVERAGE_FILE`をrepo外へ設定します。今回60/60 PASS、対象2 moduleの124 statements・68 branchesは未通過0です。private orchestration全体のcoverageや、原作の観測数ではありません。

実採取では全曲側371/375ブロックが一致し、範囲外4件を拒否しました。現在sample→owner→submissionの188件中、同じDSP点の144件とコピー完了後の44件は別集合です。実入口を加えた約18秒の短い別採取でも、同じDSP点56件は入口前、コピー完了後37件は別集合でした。後者のDSP差は1種類ではありません。コピー完了・同じDSP点・同じ原作clockを同一条件として扱ったり、固定buffer補正を導入したりしません。

原作既知128 sampleはViewerの全ブロック内に収まりましたが、その短区間内の現在sample観測は0件です。全曲側の3,325描画は142秒終端・復帰、音声3,241＋明示tail84、fault/例外0でしたが、ownerが左右各1 keyの有効区間を跨ぎ10/12のためFailedです。短い採取は計画した停止として記録し、全曲成功へ数えません。原作run89時計tupleとの自然一致、同一frame全面RGB、speaker位置の証明は未成立のままです。

## 自然音声から最終submissionまでの観測

`-AudioPcm`時の`pcm-clock.private.json`は`schema=ViewerAudioOwnerSubmission-v2`です。各音声観測について、現在のprovider frameと`FinalCompositeDrawn`通知のselected objectが同一で、出力RTが現在のtargetであることを要求します。全channelの現在owner bitが音声clockのownerと一致し、比較owner bitと整数frame対がchannel間でも一致する場合だけ保存します。欠落・stale drawは`pcmFailures`となり、前回のdrawを補完しません。

従来の時刻・source／clip・callback serialに加え、次を保存します。

- `sourceBits`、`ownerBits`、`previousOwnerBits`、`previousOwnerTime`、`currentFrame`、`previousFrame`。
- `submissionSerial`、`inputVersion`、左右・下地camera ID、出力RT IDと寸法。

IDとserialは現在のViewer lifetime内の参照です。別runの原作cameraやresource IDとは直接比較できません。`FinalCompositeDrawn`通知との対応を記録しており、音声sampleの実スピーカー出力位置やGPU完了を保証しません。`-AudioPcm`未登録時の描画経路と通常時計は変更しません。

最終ソースのmusic1001は142秒自然完走、6,591描画＝自然音声6,429更新＋明示既存時計tail 162更新。3,459 callback、左右同時66/66更新、初回late-only 1件保留、fault・例外・観測失敗0を確認しました。自然後処理の原作7段RGBを今回実行したという意味ではありません。

原作run84の実draw保存入力は、pass 5の選択・実消費PS・queue範囲・readbackまで限定資格化し、Viewer再描画の128/128画素RGBAが一致しました。別に、同じframe・threadの先行parameterでgetter→ownerの8成分がbit一致しています。ただしparameter終了後にdrawが実行され、直接のCommandBuffer生成元は未結合です。run85では既存hook境界のcaller／stackとCommandBuffer前後を追加観測しました。共通の上位呼出しとsnapshot保持だけを根拠に、記録・実行世代が同じとは判定していません。

run84の候補時計に対し、Viewer自然6,429件のsource bit・owner bit対・整数frame対が完全一致する更新は0件でした。この観測で正のsample増分の最大公約数は1,024でしたが、Unity全体の更新保証には拡張しません。補間・固定frame補正・最近傍選択による対応は作っていません。現在ソースの明示時計Control／GPU試験は528/528 PASS、両pipeline合計4,096画素の人工入力内部replay一致です。自然音声との一致や原作の同一frame RGBとは別判定です。

run82の対象区間不一致とrun83のready失敗を保存し、run84／85も外側Pendingと`complete/pins_ok=false`を保持しました。解除・drain、通常終了と所有配備復元は別に確認しています。旧StopCompletedを要求するreaderや直接nested identityのreaderは変更せず、今回の保存GPU観測は別の限定schemaで検証しました。

次の受入条件は、同一CommandBuffer実行世代から実drawへの直接対応、および原作同期cacheの実消費位置とViewer現在sample→ownerの対応です。それまで原作最終画面RGB比較を受け入れません。`OriginalVerified=false / runtime_contract=false / SourceToViewerClockContract=false / Phase257cause=Unknown`を維持します。

## 実消費時計の明示再生

原作run81では、同じSong lifetimeの既知128 sampleから同期cache・実同期API戻り値を経て、その値を消費したgetterと同じUnity frameのowner記録まで接続しました。平滑化後の8成分がbit一致しています。これは同じframeのGPU command生成やViewerの音声epochまでを証明するものではありません。

`OriginalObservedClockValidation`は、外部の明示観測を既存の`OriginalLiveClockMath`へ渡し、実Controlのowner対と対応する最終GPU submissionを確認するEditor用検証です。geometry・camera・mask入力は人工です。通常の時計、AudioSource、ライブUIへ自動登録しません。

```powershell
./Tools/OriginalLiveRendering/Invoke-IntegrationValidation.ps1 `
  -UnityExecutable <Unity executable> -OutputPath <external output> `
  -ObservedClockConsumption <external observation>
```

入力は64KiB以下のrepo外JSONです。`schema=ExplicitConsumedClock-v1`、`smoothing=Enabled|Disabled`、`sourceTime`、4要素の`state`、5要素の`parameters`を明示します。state／parametersの順序は既存`ExplicitClockCheckpoint-v1`と同じです。`expectedBits`は次の8値のfloat32 bitをsigned Int32で保持し、`currentFrame`／`previousFrame`は観測済みのControl整数frameです。privateな実測値は公開しません。

1. source time
2. smoothed time
3. correction
4. next previous-source time
5. current owner time
6. previous owner time
7. elapsed time
8. current owner timeの書込み先に保持された値

Built-in／URP14で、公開可能な人工入力は528/528 PASS（従来498＋追加30）。別実行の原作明示入力でも528/528 PASS、8成分bit一致、各2,048画素・合計4,096画素の内部RGBA replay一致を確認しました。2 pipelineは同じ原作観測の再評価であり、独立した原作観測へ合算しません。`observed-clock-*.private.json`のselected／submission serialはViewer内部の対応で、原作Unity frame番号とは別です。

先行run79のparameter未採取とrun80の早期owner型guard拒否は保持しました。既存hookの早期選択と、既に資格化したDirector field型から現在ownerを検証する経路を修正して再採取しています。原作run81の旧byte readerはseed間の保持sample field進行を拒否したままです。別のbyte輸送判定で同じ資源・世代・packetを要求し、当該fieldの単調進行を明示保持しました。これをsample時刻の連続性やPCM原点の推測に使っていません。

3 runとも外側Pendingとbodyの`complete/pins_ok=false`を維持します。28 hook解除・remaining 0・drain・code pins、通常終了後の不在と所有配備復元は別に確認しました。時計の観測は保存証拠の限定判定であり、StopCompletedや実行時held handle復旧へ昇格しません。自然142秒再生・原作GPU RGB比較は今回未再実行です。同じ原作owner更新からGPU command・camera・resource版への接続、Viewerの現在音声とのepoch、原作とViewerの同一frame RGBは未成立です。`OriginalVerified=false / runtime_contract=false / SourceToViewerClockContract=false / Phase257cause=Unknown`と歴史的Pendingを維持します。

## 記録と所有権

- 資源名・waveを明示し、同時登録は1件です。別資源・別waveは対象外です。
- 固定容量の配列へcallbackの要求・戻り件数、stream前後byte位置、thread、timestampを記録します。PCM本文は保存しません。
- callback内にUnity APIやfile出力を追加しません。streamの追加読取りも行いません。
- `AudioClip.Create`内の先行callbackを記録後、返却されたclipのidentity・寸法をmain threadで保持します。
- `Dispose`後も既存のread／set-positionは続行します。in-flight、overflow、重複attach、重複実行、例外は完了した観測として受け入れません。
- 元callbackの例外を保持します。位置を読む診断側の例外は観測失敗として記録し、実readを置き換えません。

byte位置、interleaved float件数、channelあたりの論理sample、`timeSamples`、`time`を別々に保存します。先読み位置はspeaker位置ではありません。`OriginalLiveAudioClock`のtimestampは診断用で、owner算術へ使用しません。旧constructorで作るfixtureのtimestamp 0は未計測です。

## 限定検証

人工callback検証は次の専用modeです。既存Control／GPU試験はmode指定なしで別に実行します。

```powershell
./Tools/OriginalLiveRendering/Invoke-IntegrationValidation.ps1 `
  -UnityExecutable <Unity executable> -OutputPath <external output> -StreamingAudioOnly
```

実楽曲の観測は、既存の自然再生wrapperに`-AudioPcm`を追加します。これは`-AudioClock`を必要とします。checkpointと音声終了後のtail方針は呼出し側の明示入力です。原作の開始state／音声epochを自動生成するものではありません。

```powershell
./Tools/OriginalLiveRendering/Invoke-NaturalPlaybackValidation.ps1 `
  -UnityExecutable <Unity executable> -OutputPath <external output> `
  -MusicId <selected song> -ConfirmedProducerProfile <external profile> `
  -RightTrack 0 -LeftTrack 1 -AudioClock -AudioPcm `
  -AudioClockCheckpoint <external checkpoint> -AudioClockTailToExistingDirector
```

結果の`pcm-callbacks.private.json`と`pcm-clock.private.json`はrepo外へ保存されます。前者はcallback、後者は実Controlへ採用された同じsource／clipのowner更新です。現在ロードした伴奏の内容hashも限定して確認します。privateな音源identity・時刻・source情報を公開文書へ転載しません。

## 今回の結果と境界

最終ソースの人工44/44、既存Control／GPU498/498がPASS。music1001は142秒完走し、3,459 callback、5,874音声平滑化更新、131既存時計tail更新、合計6,005描画を確認しました。初回late-only 1件は保留です。現在sampleの全5,874観測が完了済みのcallback範囲に入り、`time`はsample/frequencyのfloat32値と一致します。

原作run78と同じAWB内容・decoder情報を確認し、既知128 sampleをViewerの1 callback範囲へ対応させました。元の原作lineageは過去の別run証拠です。短い対象区間を現在sampleが直接観測した回数は0で、原作の同期cache／音声取得段階とViewerのowner段階、同一draw frame、最終RGB、speaker位置は未成立です。

最初の結合検証では、内部Pending constructorの引数追加が既存reflection fixtureを壊しました。旧constructorを保持し、timestamp未計測0として委譲する修正後に498件を再実行しました。計測項目追加でもreflection構築を互換性確認の対象に含めます。

`OriginalVerified=false / runtime_contract=false / SourceToViewerClockContract=false / Phase257cause=Unknown`を維持します。今回の実測は音声の論理座標を比較する材料であり、原作全面再現の完成率ではありません。
