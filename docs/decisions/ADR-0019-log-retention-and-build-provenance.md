# ADR-0019: archive completed logs and bind releases to clean Git provenance

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15の4 Worldだけで生ログが約474 MiBとなり、無圧縮directoryの線形増大が運用上の問題になった。また、assembly informational versionが基底commitだけを示し、dirty worktree由来のbinaryとclean commit由来のbinaryを区別できなかった。完全Configとseedがあっても、実行コードを一意に特定できなければ再現性は成立しない。

旧releaseの全生ログを継続保存する要件はなく、保存価値がある実験は`research/`へ要約する既存境界がある。

## Decision

- 観測中のWorldだけを通常directoryとして保持し、完了時に同release directory内の`world-NNNN.zip`へ圧縮する。
- archiveは一時fileへ作成し、entry名・件数・非圧縮sizeを元directoryと照合してから確定する。確定後にSHA-256 sidecarを書き、最後に元directoryを削除する。
- World番号はdirectoryとZIPの両方から最大値を求め、archive後も再利用しない。
- Observation App Configで、完了World圧縮と現行release以外のlog directory削除を明示的に切り替えられるようにする。v0.15以降の既定値は両方trueとする。
- Build時にfull Git commitと`clean` / `dirty` tree stateをassembly metadataへ埋め込む。
- release publishはclean treeを既定条件とし、dirty buildは明示的な診断用途だけ許す。
- `run.json`へcommit、tree state、Config hashを保存し、publish成果物へartifact hash付きmanifestを保存する。
- 生ログとarchiveは引き続きGit管理外とし、Gitへは要約済み研究結果だけを残す。

## Consequences

- 長期logのdisk消費を減らしつつ、World単位の完全な生ログを保持できる。
- 圧縮失敗時は元directoryが残るため、保全側へ倒れる。
- archiveされたWorldをAppが直接再開・閲覧する機能は追加しない。snapshotと再開形式は引き続きDraftである。
- 旧release logはConfig既定により削除されるため、残す価値があるrunは事前に`research/`へ要約する必要がある。
- clean commitへ紐づかないdirty buildは診断可能だが、正式releaseの再現根拠にはできない。
