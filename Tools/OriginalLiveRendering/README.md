# 公開用の限定GPU検証

人工texture、独立したalpha ramp式、明示cameraの背景色だけを使用します。原作assetやprivate captureは不要です。元の研究用Viewerを開かず、Fork内のソースから新しい小規模Unityプロジェクトを準備します。

Unity 2022.3.62f3、Windows D3D11、URP14.0.12を使用します。`Prepare-ValidationProject.ps1 -ProjectPath <新規の外部ディレクトリ>` を実行してください。既存ディレクトリやrepo内への生成は拒否し、scene・ProjectSettings・Packagesを既存Viewerへ書き込みません。

準備したプロジェクトをUnityの `-batchmode -force-d3d11 -projectPath <準備先> -executeMethod OriginalRenderingValidation.Run -validationOutput <repo外の結果先> -logFile <repo外のログ先>` で実行します。GPU検証なので `-nographics` は使用しません。処理自身が終了コードを返すため `-quit` は不要です。

検証範囲：

- renderer／coverage shaderのコンパイルとGPU実行。
- 同じ入力RTを更新する右→左→下地alpha合成、次frame再利用、状態復元。
- 独立した数式によるcoverage alpha、Y反転、入力alpha非依存。
- cueのk/j、ratio、raw pass、未知入力、既知producer算術。
- Built-inとURPの実camera描画、sourceの状態・buffer保持。
- 従来 `CompositeThree` の変更なし比較用コピーによるGPU smoke。

この小規模プロジェクトはengine側5クラスをコンパイルします。実 `LiveTimelineControl`、timeline bridge、session、registration、resolverの全結合をstubで代替しません。本家起点のコンパイル問題が解消するまで、Fork全体のUnity統合受入は未完了です。

結果 `summary.json` は実行環境・件数・成否だけを含みます。UnityログやLibrary／Temp／生成結果はcommitしないでください。過去のprivateデータ照合と今回の人工テストは [PROGRESS.md](../../PROGRESS.md) で区別します。
