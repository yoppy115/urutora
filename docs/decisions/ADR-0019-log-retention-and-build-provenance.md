# ADR-0019: archive completed logs and bind releases to clean Git provenance

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

長期Runの無圧縮ログは線形増大し、Version名だけではdirty worktree由来binaryとclean commit由来binaryを区別できない。ConfigとSeedだけでなく実行コードを一意に特定する必要がある。

## Decision

- 観測中Worldだけを通常directoryで保持し、完了時にrelease directory内のWorld別ZIPへ圧縮できる。
- archiveは内容・件数・非圧縮sizeを検証し、hashを記録してから元directoryを削除する。
- World番号はdirectoryとarchiveの両方から決め、再利用しない。
- Buildへfull Git commitとclean / dirty stateを埋め、正式publishはclean treeを既定条件とする。
- RunへVersion、repositoryCommit、tree state、Config hash、Seedを保存する。
- 生ログとarchiveはGit管理外とし、保存価値のある結果だけをresearchへ要約する。

## Consequences

長期保存量を抑えつつRun provenanceを保てる。圧縮失敗時は元directoryを残す。snapshot / archiveからの再開形式は引き続きDraftである。

