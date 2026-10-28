# Viewer streaming音声の限定観測

`StreamingAudioCapture`は、既存のstream callbackを明示登録したときだけ記録します。元音源を再解析せず、従来のsample providerとstream Positionを利用します。通常UIへの自動登録はありません。

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
