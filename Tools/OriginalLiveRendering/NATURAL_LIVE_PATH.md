# 自然ライブの接続経路監査

更新日：2026-10-22。前半は `263384d` の静的調査です。末尾に、その後実行した人工入力のcamera選択GPU検証を区別して記録します。原作採取や既存ライブへの合格条件の追加ではありません。35チェックと実Control結合42チェックは独立した成果として保持します。

## 既存のロードと更新

```text
UmaViewerBuilder.LoadLive
  -> 既存のpreloadとLiveSceneロード
  -> Director.Initialize
  -> LoadLiveUma
       -> LoadNormalUma / LoadMobUma
       -> UmaContainerCharacter.LoadPhysics
  -> Director.InitializeUI / InitializeTimeline / InitializeMusic / Play
  -> Director.Update
       -> OnTimelineUpdate -> Control.AlterUpdate
       -> ApplyTimelineLateUpdate -> Control.AlterLateUpdate
            -> Bridge.CompleteFrame -> RenderSession
       -> UpdateMainCamera
```

原作方式の登録は、ロード済みControlに対する既存の `OriginalLiveTimelineRegistration` を利用できます。track選択、k/j、描画先、入力providerは呼出し側の明示値です。自動的に通常ライブを置換する必要はありません。

## native物理の依存

`LoadNormalUma` と `LoadMobUma` は、キャラクタ構築中に引数なしの `LoadPhysics()` を呼びます。有効なphysics containerがある場合、ここからcontroller作成、`LoadFromDataContainers`、Bind、native cloth設定、Resetへ進みます。

`EnablePhysics` は `AlterLateUpdateRuntime`／`AlterLateUpdatePostRuntime` のsimulation呼出しを制御しています。`LoadPhysics()` 冒頭の条件には含まれません。また `SetDynamicBoneEnable(false)` も、既にあるcontrollerの状態を変える処理です。したがって、既存の物理OFF操作だけでnative初期化を省略できるとは判定しません。

検証cloneのDLL隔離状態は維持しています。DLL復元・保護設定変更・仮のnative実装による置換は行いません。自然ライブをnative物理なしで試す場合、既定動作を保持した明示的なロード経路が別途必要です。今回その動作変更は実装していません。

## 自然な描画値の供給

既存Controlから選択key、使用ratio、raw thickness、色、AA、offset、rotation source、kind、raw pass、選択cameraを受け取る経路はあります。

一方、今回確認したコードには次の値を自然更新から生成してresolverへ供給するproducerはありません。

- 評価済みfade
- 有効material thickness
- shader-domainの角度
- 実UV／ST／texel Y／mip biasを含むTextureMapping

`OriginalLiveDrawInputResolver` は明示providerを呼ぶ接続点です。これらを自動生成する実装ではありません。raw thicknessの代用、固定角度換算、fade=1、固定FPSによる時間変換で補完しません。過去の原作実測snapshotを明示供給する経路と、未観測frameの自然な生成は区別します。

## main cameraの切替順序

Directorの通常Updateでは、Controlのlate updateとbridge通知の後に `UpdateMainCamera()` が呼ばれます。`MainCameraTransform` の更新はその後段です。

そのため、登録時の下地camera resolverで単純に `Director.MainCameraTransform` を読む方式は、同じupdateでcameraが切り替わった場合に直前のcameraを読む可能性があります。これは静的な順序から分かるリスクで、自然ライブでの誤描画を観測したという意味ではありません。

接続候補は、既存 `OnUpdateCameraSwitcher` が通知した選択を保持し、既存Controlのcamera参照と明示したfree cameraから下地を選ぶ方式です。未通知状態やfree-camera分岐を推測で埋めず、`Camera.main` のfallbackも追加しません。Directorや9クラスの既存実行順は変更していません。

## 現在の境界

自然ライブrunは未実施です。原作ゲーム、hook、ARM、collector、配備は操作していません。native物理の読込依存と描画値の自然供給を、人工入力42チェックとは別に扱います。

基盤修正を描画ブランチへ一括マージせず、成功済み `781ef89` と結合検証済み `263384d` を参照できます。原作S生成camera、自然helper、全frame・全画素の一致、過去のPendingManualRecoveryは未確定のままです。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。

## 同一Control更新でのcamera選択検証

静的監査後、既存の公開結合環境にcamera選択52チェックを追加し、従来42件と合わせて **94/94 PASS**。Unity 2022.3.62f3／D3D11、Built-in／URP14の人工typed key・geometryによる結果です。

実 `Director.InitializeTimeline` でcamera登録と `OnUpdateCameraSwitcher` のDirector handlerを設定しました。選択側のclosureは通知されたindexを保持し、描画時にControlのcamera参照を引き直します。indexが負なら明示したfree cameraを使用します。未通知状態を0や `Camera.main` で埋めません。

|切替通知のindex|bridgeで選択したcamera|bridge時のDirector main|後段の実main更新後|
|---|---|---|---|
|0|camera 1|camera 1|camera 1|
|1|camera 2|camera 1|camera 2|
|2|camera 3|camera 2|camera 3|
|-1|明示free camera 0|camera 3|camera 0|

これは人工fixtureの連続したControl更新の順序です。両pipelineの4ケースすべてで、その呼出し中の出力中央色が選択したcameraのgeometry色に完全一致しました。切替通知がControl処理中、描画がそのlate update完了後であることも確認しています。rendererはsource cameraの状態とDirector main参照を変更せず、後段の実 `UpdateMainCamera` がcamera nodeを切り替えます。

camera未通知、範囲外index、free camera未指定では、新レンダラの既存 `MissingSelectedCamera` により描画せず出力を保持しました。Directorの既存の選択保持・free camera分岐はそのままです。これは明示登録の利用例であり、通常ライブに新しい開始条件を追加するものではありません。

この選択問題については、既存イベントとControl参照だけで同一更新のcameraを選べました。今回のfixtureでは、bridge通知を移動したり、追加の後段通知を設けたりする必要はありませんでした。追加はEditor検証と文書だけで、描画9クラス・2shaderとDirector／Controlの実装は変更していません。

reflectionで人工scene参照を与え、後段の実main更新を呼んだ限定試験です。Directorの通常 `Update` 全体、実楽曲、character animationやpost effectの描画準備完了は検証していません。自然provider、native物理、原作S生成cameraの正式帰属、全frameの最終画素対応は引き続き未確定です。過去の判定flagや `PendingManualRecovery` は変更しません。

## 有効thicknessの部分接続

保存資料のproducerとcallbackを照合し、有効thicknessの分岐を既存math／resolverへ追加しました。raw cueは同じControl更新の選択済みsnapshotから受け取ります。固定値・下限の定数は明示入力で、kind 0の時間依存状態は明示されたreceiver分岐がない限りUnknownです。既存constructorと評価済み入力の優先を維持しています。

Unity f3／D3D11の人工GPU検証は152/152 PASS（従来94＋厚さ算術12＋provider結合46）。Built-in／URPで各6ケースの全RGBAが従来の評価済み入力経路と一致しました。これにより、補間済みraw値→明示依存による有効値→既存coverage・順次合成の接続を確認できました。

自然provider全体は未完成です。固定値・下限の実定数、kind 0の実状態、自然fade、角度helper依存、TextureMappingは別途確認・供給が必要です。人工定数を原作の確定値に昇格せず、未観測frameへの保存値延長も行いません。原作採取・ゲーム操作・native DLL復元は0で、通常ライブへの自動登録や新しい開始条件は追加していません。
