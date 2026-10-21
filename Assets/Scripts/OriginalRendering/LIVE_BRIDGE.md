> 研究環境から移植した設計・使用法です。過去の検証と、このForkで今回実施した検証は [PROGRESS.md](../../../PROGRESS.md) で区別しています。

# Viewer timelineと原作方式レンダラの明示接続

`OriginalLiveTimelineBridge` は、既存 `LiveTimelineControl` が実際に選択したcurrent/next keyと、実際に使用した補間ratioを受け取ります。key選択helperや補間時間計算を再実行しません。`AlterLateUpdate` のcamera・lookat・transform更新後に、そのupdateのimmutableなsnapshotを通知します。

## 既存動作との関係

`LiveTimelineControl.OriginalCompositeBridge` は既定でnullです。sceneやDirectorへの自動登録、既存cameraの自動有効化、既存 `CompositeThree` の置換はありません。旧multi-camera処理の命令順序と既存material更新を保持します。

新経路を使う場合だけbridgeと `OriginalLiveRenderSession` を作成し、明示的な下地・右・左camera、出力RT、入力値resolverを渡します。track ordinal `k` とComposite index `j` は `BindTrack(k,j)` で独立に指定し、左右用trackもsessionの引数で指定します。`j` から左右やcameraを推定しません。

```csharp
using Gallop.Live.Rendering;

var bridge = new OriginalLiveTimelineBridge();
bridge.BindTrack(rightTrack, rightCompositeIndex);
bridge.BindTrack(leftTrack, leftCompositeIndex);
control.OriginalCompositeBridge = bridge;

var session = new OriginalLiveRenderSession(
    bridge, compositeShader, coverageShader,
    rightTrack, leftTrack, baseCamera, rightCamera, leftCamera, output,
    selection => OriginalCompositeCueAdapter.FromEvaluatedProducer(
        selection.Cue.Source, selection.Cue.Values,
        ResolveConfirmedDrawState(selection),
        ResolveExplicitTextureMapping(selection)));

// 利用終了時。source cameraとoutputの所有権は呼出し側にある。
session.Dispose();
if (ReferenceEquals(control.OriginalCompositeBridge, bridge))
    control.OriginalCompositeBridge = null;
```

上記Resolve関数は呼出し側が実装する明示入力です。原作の未確定helperを実装済みとする関数ではありません。shader参照もInspector等から保持し、Player buildのshader除外を避けてください。

## cueと未確定値

Viewerのtyped keyにあるthickness、line color、antialiasing、mask offset、rotation source、kind、raw passをadapterへ渡します。これはViewer fieldの接続であり、原作native ABIとの正式な同一性を新たに証明するものではありません。

fade、有効thickness、shader-domain z/w、ST、texel Y、mesh UV、mip biasはresolverから明示供給します。`maskRoll/360`、固定FPSによるfade時間換算、kindからの推測変換はありません。未供給なら `UnresolvedDrawInputs` とflagsを残し、新経路の描画を行いません。これは既存ライブや実機採取の開始条件を追加するものではありません。

key tokenはkey objectの同一性を保持するinstance内のopaque IDです。原作アドレスではありません。`ViewerFrame` は既存Controlが渡した値であり、ゲーム内部時刻や原作のUnity frameへ換算しません。通知handlerの例外は分離して記録し、後続handlerと既存updateを継続します。

## camera producer

`OriginalCameraProducer` は無効状態の所有cameraへsourceのcore設定とposeをコピーし、下地RTと共有入力RTを描画します。source cameraのenabled、targetTexture、`SetTargetBuffers` を書き換えません。下地を保持し、右camera→coverage→右描画→同じ入力RTへ左camera描画→coverage→左描画→FinalCompositeの順序で呼びます。

Built-inでは `Camera.Render`、URP 14では `SingleCameraRequest` と `RenderPipeline.SubmitRenderRequest` を所有cameraに使用します。元Viewerのpipeline設定は変更しません。sourceのcamera script、post effect component、AdditionalCameraData、custom renderer設定は複製しません。URPは現在のpipelineのdefault rendererを使います。

**BREAKING RISK（新経路の明示利用時のみ）:** cameraの追加描画は処理負荷と描画callbackを増やします。source固有のpost effect、camera stack、custom rendererが必要な映像は、このcore camera copyだけでは同一になりません。非破壊の代替はbridgeをnullのまま維持すること、または既存 `RenderSequential` に別の明示producerを供給することです。通常ライブの出力先は自動で切り替えません。

bridge/sessionはUnity main threadから使用します。`Dispose` は通知解除と所有RT・camera・materialの解放を行い、source cameraと外部outputを破棄しません。`LastStatus`、`LastUnresolved`、`LastException` で新経路の状態を確認できます。

## 検証と境界

分離Unity環境では、実Viewerソースをコンパイルしたassemblyの実 `AlterLateUpdate` をtyped timeline fixtureで実行し、key選択・ratio・late update順序と、明示3cameraが描いた実geometryからcoverage・順次合成まで検証します。テスト用Controlへの置換やkey選択helperのstubは使用しません。

これは実楽曲の自然再生、原作S生成cameraの帰属、元Viewer editor versionでの通常ライブ採用、全frame一致の証明とは区別します。保存画像によるreplay、既存cue adapter、coverage、`CompositeThree` の成功結果は保持します。原作実測alphaの残る9点の1/255差も未解消です。

`OriginalVerified=false`、`runtime_contract=false`、`Phase257cause=Unknown`。原作S生成cameraは `Unknown` のままです。

## ロード済み実楽曲のtrack登録

`OriginalLiveTimelineRegistration` は、ロード済みControlのworksheetから、呼出し側が指定した左右trackの `MultiCameraNo` を取得して `BindTrack(k,j)` へ渡します。kをjへ代用しません。実楽曲の保存データに存在するこのfieldを、Viewerのtyped groupにも保持するよう追加しました。原作native fieldの正式な帰属やcameraの同定を、このfield名から推定しません。

```csharp
var registration = new OriginalLiveTimelineRegistration(
    control, rightTrackOrdinal, leftTrackOrdinal,
    frame => explicitBaseCamera,
    compositeShader, coverageShader, output,
    selection => ResolveConfirmedEvaluatedInputs(selection));

// 終了時。自身のbridgeだけを解除し、既存timeline処理を残す。
registration.Dispose();
```

左右の描画cameraは、各updateでControlが保存した `Selection.ViewerCamera` を使用します。`SetMultiCamera` でcameraが交換されても次の選択結果に追従し、再登録は不要です。下地cameraは明示resolverで供給します。出力は明示RTのままで、通常ライブの最終出力を自動置換しません。既存bridgeが登録済みなら、その所有権を奪わずconstructorから通知します。

`MultiCameraNo` を持たない旧データや独自データでは、初期値0を原作の確定情報として扱わず、`TrackBinding(k,j)` を渡す既存の明示指定constructorを使用してください。**BREAKING RISK（serialized schema）:** typed groupのJSON出力には追加fieldが含まれます。旧JSONの列集合を固定した外部consumerを維持する場合は、明示TrackBinding経路を利用し、そのconsumerへ新schemaを無条件に渡さないでください。既存描画処理は追加fieldを読みません。

分離URP14/D3D11では、保存された実楽曲のposition key12件・lookat key8件を実Controlへ供給し、公開AlterUpdateとAlterLateUpdateを通して、key境界・curve・camera交換・登録解除を検証しました。確認済みの保存状態とraw offsetが一致するViewer frameでは、実cameraが描いた検証用geometryからの最終RGBが既存9,216点と一致しています。異なる時点の状態を外挿せず、未観測の評価値はUnresolvedのままです。これは実楽曲のキャラクタ・ステージを自然な実時間で再生した検証とは区別します。