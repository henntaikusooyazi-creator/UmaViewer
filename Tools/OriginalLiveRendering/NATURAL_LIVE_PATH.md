# 自然ライブの接続経路監査

更新日：2026-10-22。`263384d` のソースを確認した静的調査です。新しい実行試験・原作採取・追加の合格条件ではありません。35チェックと実Control結合42チェックはそのまま保持します。

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
