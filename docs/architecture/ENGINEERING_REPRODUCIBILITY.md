# Engineering and Reproducibility

Status: **Engineering / Reproducibility Canon**

この文書はゲーム内法則ではなく、Runを再現・比較し、長期観測を安全に運用するための開発境界を定める。

## Required facilities

- GitHub Actionsによる自動検証。
- SimRunnerによる決定論的record / replay。
- FsCheck等によるproperty-based test。
- release別のWorld Log連番。
- 完了World Logの圧縮・retention。
- Git provenanceと`repositoryCommit`の記録。
- 権限問題を限定的に解決する管理者finalizer。

実装方式は交換可能だが、同じCode / Config / Seedの意味論を変えず、失敗時に再現情報を残す。

## Run envelope

最低限、`Version`、`repositoryCommit`、Config識別子または内容hash、RunSeedを保存する。形式だけの同一Versionを比較単位にせず、commitの異なる結果を明示的に分ける。

Observation cache、NPC近傍index、Held InformationのFIFO索引、CPU並列化、ログflush / diagnostics間隔、UI描画頻度は非権威的な最適化である。Communicationの抽選は取得順を権威的FIFO stateとしてnamed RNG streamで一様抽選し、Dictionary列挙順を使わない。これらの有無、thread scheduling、非権威的collection order、CPU core数によってSimulation Event列を変えてはならない。

## Log operations

ログflush間隔はEngineering / Operations設定であり、世界ルールではない。正式保存時はrelease / commit provenanceを付与し、圧縮内容を検証してから元ログを削除する。生ログを正史docsへ混入させない。
