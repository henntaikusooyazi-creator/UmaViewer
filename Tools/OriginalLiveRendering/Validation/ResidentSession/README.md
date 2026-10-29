# 同一プロセスでの採取ジョブ切替

採取結果の受入と、次のジョブへ進める停止状態を分離する汎用部品です。原作固有のloader、hook、アドレス、採取データ、配備設定は含みません。通常Viewerのrenderer、clock、UIには接続しません。

`resident_session.hpp` は渡されたPIDと生成時刻、重複しないjob ID、明示binary hashの書式、最大8ジョブを検査します。実ファイルのhashやcontroller identityの確認は呼出し側の責任です。呼出し側は操作を直列化し、ロード済みジョブでは次の全条件を実測してから `retire` します。

- 対象workerの終了、handle identity、観測handle閉鎖、worker registry閉鎖。
- hook無効化、実行中callbackのdrain、code復元、所有resource解放、当該libraryの論理retire。

欠けた条件をcaptureの成功やcontrollerの終了で代用しません。`retire` が失敗したsessionは再利用を拒否します。libraryの論理retireはDLLのunloadや全heap解放を意味しません。ロード前の取消は、呼出し側が未ロードを直接確認できた場合だけ別経路で扱います。`control_cancel.py` はARM前の明示取消を現在のrunとprocess lifetimeへ結び、boolを整数identityとして受け入れません。

`OwnedFileUpgrade.ps1` は、明示された所有ファイル1～3件だけをhash照合・backup後に更新するライブラリです。実プロセス不在の確認は呼出し側が提供し、各書込み前にも検査します。途中失敗時は所有権と不在を再確認してrollbackし、それができなければ `PendingManualRecovery` を残します。外部から同時に起動・変更される状況の排他保証や、自動でゲームを終了する機能はありません。

## 今回の確認範囲

同じ原作ゲームPID・生成時刻のまま、3ジョブを順次実行しました。ジョブ間のゲーム再起動は0で、3件ともnative worker終了・handle／registry閉鎖・hook停止／drain／resource retireの証拠から再利用可能と判定できました。最初の取消と次の採取は外側Pendingのままです。最後は `StopCompleted`／保持handle復旧が成立しましたが、先頭getterと同じframeの対象parameter保存は0件で、描画世代joinや新しいRGB一致には数えません。

初期試作では送信失敗時に終了証拠をreceiptへ移す前にreturnしていました。証拠を送信より先に確定する最小修正後、上記3ジョブで確認しました。旧試作receiptは変更していません。また、実機で使用した版の制御待機10分が準備中に切れ、次のジョブは接続前Pendingになりました。制御待機だけを8時間へ延ばした版を別にビルドし、現在のゲームを閉じず、通常終了後だけ所有配備を更新する待機処理を用意しました。8時間の実機soakと、その版による次の実採取は未実施です。採取窓、read／byte／hook／collector上限は拡大していません。

**BREAKING RISK（所有observer配備の明示変更）**：one-shot終了後にDLLを終了させる運用から、上限付きで停止済みmoduleを保持する運用へ変わります。通常Viewerを変更せず、現在のゲームへの外部attachや強制再起動も行いません。代替は従来の単発採取を維持することです。8件の上限を越えた再利用や、取消だけを根拠とする未確認workerの再利用は対象外です。

## 閉鎖検証

Windows x64／Visual Studio C++を使用します。出力先には新しい専用directoryを指定してください。ゲームやUnityは起動しません。

```powershell
pwsh -NoProfile -File .\Build-Closed.ps1 -OutputDirectory <new-native-output>
pwsh -NoProfile -File .\Test-OwnedFileUpgrade.ps1 -OutputDirectory <new-upgrade-output>
```

状態遷移131チェック、同じ自作Win32 hostでの2ジョブ切替36チェック、所有ファイル更新42チェックを確認しました。Win32 hostは自作DLLを保持し、ゲームへのload-pathや原作描画の証明にはしません。別の私有watcher試験では正常終了・異常終了・取消の3 lifetime、9チェックがPASSです。

Pythonは既存uv管理環境で、対象2ファイルに対する `ruff check` の後、関連 `pytest` を実行します。cache、pytest一時出力、`COVERAGE_FILE` は専用directoryへ向けます。

```text
ruff check control_cancel.py test_control_cancel.py
pytest -q --cov=control_cancel --cov-branch test_control_cancel.py
```

24件PASS、対象moduleの10 statements／6 branchesは未通過0でした。これは取消validatorの検証であり、collector全体のcoverageではありません。過去のControl／GPU 636件、142秒Viewer自然再生、原作RGB replayは今回再実行していません。

`OriginalVerified=false`、`runtime_contract=false`、`SourceToViewerClockContract=false`、`Phase257cause=Unknown` と歴史的Pendingを維持します。
