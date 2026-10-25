# CPU Film・owner値・自然描画入力の対応

検証ブランチの限定結果です。通常ライブへの自動採用、原作同一frameの全画面一致、時刻契約の成立を認定するものではありません。

## Filmの再生成

| 対象 | 入力の対応 | 再生成・比較 |
| --- | --- | --- |
| 通常Add | 新しい同一原作更新のCPU parameter、安定したowner値、実pass／keyword／programとGPU消費 | 実Controlで補間後Filmを再評価。消費24/24成分がfloat32 bit一致、128/128画素RGBA一致 |
| inverse VignetteAdd | 過去の独立runの同じUnity frameにあるCPU parameterとGPU消費 | 消費17/17成分に一致するlayerの値の対応が一意。CPU Filmから128/128画素RGBA一致 |

比較はBuilt-in D3D11です。texture、geometry、samplerとFilm以外のGPU入力は対象drawの観測値を保持しています。新しいAdd観測と過去inverse観測の再評価を、独立した新規観測数へ合算しません。inverseではowner値を採取していないため、keyの区間から評価時刻を推測していません。

対象PSの実bytecodeからsource swizzle、出力mask、内積の幅を照合し、消費成分を限定しました。CPU側の全Film値との比較も保存しています。通常Addに残る未使用laneの差を消したとはせず、全CBのbit一致や別programでの正しさへ拡張しません。shaderへのepsilon・出力補正は追加していません。

既存のControl、producer math、program selection、registry、chainを使用しました。今回の公開変更には、私有collector、候補shader、実key、採取値、native layoutを含めません。

## 時刻の対応

通常Addの対象更新では、前後で安定した原作owner比較値を実Controlの評価入力とし、実楽曲のkeyと補間から生成したFilmが原作CPU／GPUの消費成分に一致しました。Unity frameからowner値を換算せず、欠落を前回値や音声位置で埋めていません。

これは一点の対応です。継続する原作stream、再生開始点、seek／pause後のepoch、音声位置とViewer更新の対応は未確定です。

限定metadataと同一buildの静的コードから、時刻controllerが音楽時刻、平滑化後の値、現在時刻、差分時刻を分けて更新することを確認しました。現在時刻の更新内には音楽時刻へ渡す値の取得分岐と平滑化・pause条件があり、直接呼出し先から音声playbackの時刻getterを指すnative import宣言まで追跡しました。実行時の解決先とplayback IDは未採取です。この経路確認を、同一更新の音声位置・controller state・Control owner値の実測joinとは扱いません。

## mask・projection・camera

typed receiverから描画callbackを特定し、連続するunwind断片を限定読取りしました。直接参照されるBlit経路ではUV scale／offsetの引数生成を確認しました。直接calleeは `StaticOnly` で、新しい同一drawのcommand traceやshader identityの証明ではありません。

UV引数から、native側の頂点生成・補間、textureの向き、shaderが消費するmapping全体を確定することはできません。Film fullscreen mappingを自然maskへ流用する実装は追加していません。

中央下地cameraとprojection constructorは未確定です。camera記録内の数値だけで正式なIDを決めず、対象resource版、同一queueのwriter範囲、render-taskとconsumerを結ぶ必要があります。

## 停止・保全と試験の範囲

正常停止runではStopCompleted、同一保持handleによる復旧、hook復元、drain、pin照合、通常終了後の所有配備復元を確認しました。途中のmetadata上限拒否と接続切断はPendingを維持します。接続切断runの原作終了原因はUnknownで、hook解除成功や通常終了とは主張しません。プロセス不在後の所有配備復元は別のreceiptです。

ゲーム未起動の準備不成立も保存し、成功runへ置換していません。読取・件数・GPU時間の既存上限を維持しました。保護対象のCoreファイルと既存DLLの未stage削除は保持します。

私有RGB比較とreader／admission試験を、公開人工GPU試験、既存Control結合、142秒自然再生の件数へ加算しません。今回は公開runtimeを変更していないため、公開Controlと142秒自然再生の反復は実施していません。

## 次の受入条件

1. CPU入力の値の対応に加え、対象camera／layerの実行経路を同一更新の描画へ結ぶ。
2. 再生位置の取得元、時刻controller state、Control owner値、Viewer側音声位置を同じ更新に結ぶ。
3. 自然projectionとmaskの頂点／補間入力を、確認したBlit引数とGPU消費へ接続する。
4. 中央下地の対象resource版とwriterを、一意なrender-task／cameraへ結ぶ。
5. 対応した原作frameとViewer出力を、同じ解像度・処理段階の全画面RGBAで直接比較する。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` を維持します。
