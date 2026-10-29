# 最初の実parameter frameの監査

先頭getterの後に実際に記録された最初のparameterを選び、その**同じframe**のgetterと対応付けます。入力は呼出し側が資格化した観測です。raw native layoutの解釈や採取、Viewer runtimeへの登録は行いません。

`select_first_frame` は次の順序を守ります。

1. 読取上限・記録範囲・run/lifetimeを呼出し側で資格化する。`window_complete` の省略時はUnknown。
2. ordinal 1の完了getterより後にある、最初の実parameterを時系列で選ぶ。同じ開始時刻の複数候補はUnknown。
3. そのframeにgetterが1件だけあり、正常完了・同じthread・owner・owner値のbit列・安定性が一致する場合だけ進む。
4. 次ordinalのgetterで選択frameの終わりを確認する。ordinal欠落、同frameのエラー／未完了getter混在、owner/lifetime変更はUnknown。

最初のparameterが不成立でも、後続の正常なparameterや近いframeへ選び直しません。固定sample補正、時刻の最近傍、同じkeyを代用条件にしません。getterとparameterの各保存上限は16です。型不正・異なる観測scopeは例外で拒否します。

`Matched` はこの限定された対応検査の結果です。CommandBuffer generation、GPU draw、自然音声epoch、camera／resource版、最終RGBA一致、runtime contractの成立を意味しません。getterのowner値を計算した根拠と観測窓の完全性は、別の私有readerで検証します。

## 今回の準備と確認範囲

新しい私有採取profileでは、metadata・期限・既存上限を確認してからframe選択を確定する順序へ修正しました。同frameにエラー付き／未完了getterが混在しても、正常1件だけを拾いません。旧profileと旧失敗結果は保持しています。

- 実際のnative関数を抽出したCPU試験：新profile 21、従来profile 6チェックPASS。GPU contextや原作関数を呼びません。
- 新collector：524,288 bytes、source graph 128、入力lease上限検査PASS。各上限・hook対象は拡大していません。
- 公開監査器：53件PASS、100 statements／54 branchesの未通過0。
- 私有の時計・generation readerとの結合を含むPython：120件PASS。公開53件を含み、独立観測数へ加算しません。私有adapterは72 statements／24 branchesの未通過0です。
- 過去の保存窓の再評価：上限到達はUnknown、先頭frameのparameter 0件もUnknownのまま。過去のcopied-generation保存証拠も再評価しましたが、新しい原作観測やGPU RGB再描画ではありません。

移設した既存readerの依存module漏れを初回試験で検出しました。必要な限定依存と保存済みsidecarだけを別rootへ揃え、元の判定条件を変えずに再検証しています。

現在のゲームは終了していません。旧resident listenerは待機期限で終了済みで、新jobはまだ接続・ARMしていません。8時間待機版の通常終了後更新を待つ状態です。今後の実採取ではPlay前に観測準備を完了し、最初の実parameter frameをこの方針で検査します。その後も、同一frameのgetter→owner→parameter→copy generation→GPU draw、Viewerの自然source世代→owner→submission、同一最終出力の資格化を順に要求します。全面RGBA比較は未実施です。

## 実行

uv管理の環境で、対象を限定してRuff、pytestの順に実行します。cache、一時出力、`COVERAGE_FILE` は新しい専用directoryへ向けます。

```text
ruff check first_render_frame.py test_first_render_frame.py
pytest -q --cov=first_render_frame --cov-branch test_first_render_frame.py
```

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。物理・IK・手つなぎの変更は行っていません。
