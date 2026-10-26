# 明示checkpointからの音声時計

`OriginalLiveSmoothingClock` は、`OriginalLiveClockMath` の結果を実Controlのowner更新へ結び付ける任意登録です。初期stateとParametersは呼出し側がすべて明示します。原作の開始epochやPause／Seekのreset規則を推定しません。

計算した次stateは一旦保留します。同じ音声source・clip・Unity frame・sample・owner時刻が `OriginalLiveAudioClock.LastObservation` へ結合された場合だけ、`CommittedState` を進めます。owner不一致、更新未採用、登録置換、clip切替、Disposeでは保留値を破棄します。GPU側の描画成功とは別に、Controlへの採用を確定条件とします。

sourceを直接渡すconstructorのほか、明示resolverを渡すconstructorがあります。後者はtimeline登録時に音声がまだロードされていなくても待機し、最初の有効なsampleでsource／clipを一度だけ保持します。後の切替を自動resetせず、呼出し側の `Reset(currentSource, state, parameters)` を要求します。既存の3引数／4引数 `OriginalLiveAudioClock` は変更していません。

## 自然再生の限定検証

`Invoke-NaturalPlaybackValidation.ps1` の `-AudioClockCheckpoint <外部JSON>` は、`-AudioClock` と併用します。JSONはrepo外・64KiB以下で、次の全要素が必要です。

- `schema`: `ExplicitClockCheckpoint-v1`
- `smoothing`: `Enabled` または `Disabled`
- `state`: previous source time、smoothed time、correction、current owner timeの順の4要素
- `parameters`: reference step、dead zone、correction limit、reset distance、maximum timeの順の5要素

欠落要素をゼロで補いません。checkpointは明示された計算入力であり、現在のViewer音声と原作音声のepochが一致する証明ではありません。保存した途中更新のcheckpointを別の再生開始へ与え、確認済みのdiscontinuity分岐を通す検証も、原作の開始stateの証明とは区別します。

summaryではraw source timeとowner timeを別々に保存し、stateの確定件数、破棄件数、保留件数を記録します。音声終端後に既存Directorへ戻す既存の明示tail policyは、その区間を別件数として保持します。

## 検証結果

公開人工Control／GPU検証はBuilt-in／URP14で492/492 PASSです。従来456件に、評価だけではstateを進めないこと、異なるowner・clip・source・登録、未採用更新、reset、Disposeの36件を追加しました。

最終ソースの自然再生は142秒、3,371/3,371描画、左右同時有効25/25更新で自然終端・Viewer復帰まで完了しました。平滑化stateの確定は音声に結合した3,288更新で成立し、破棄・保留・owner不一致は0件です。残り83更新は音声停止後の既存Director時計による明示tail区間です。初回late-onlyの1通知は別に保留しています。

このrunの後処理は `ArtificialSevenStageDiagnostic` です。自然color・depth・typed payloadによる350段、35回の段別RGB比較、4,480画素の最大差1byteを確認しました。原作shaderによる自然RGB一致や、原作同一frame比較には拡張しません。

原作側の追加保存証拠では、実消費getterからの時刻算術を再評価しました。ただし外側controllerは追加metadataのschema拒否でPendingを維持し、保存bodyの独立再検査を実行時の保持handle復旧成功へ読み替えていません。対象drawのframe記録は空で、時計からGPUへの同一frame結合は未成立です。開始stateのwriterと音声epochも未確定のままです。先行するARM待機上限・ロード中停止の記録も保持しています。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` は維持します。原作の全writer閉包、自然projection、TextureMapping、同一原作frameの最終RGBを、この時計登録から確定しません。
