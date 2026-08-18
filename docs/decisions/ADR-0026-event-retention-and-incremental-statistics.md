# ADR-0026: separate recent events, statistics, milestones, and raw archives

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

全Eventを無期限にCoreへ保持し、UIや集計のたびに再scanすると、長期RunのMemoryと処理時間が履歴量へ比例する。NPCの有限Memory、プレイヤー向けの最近ログ、Game内統計、将来のHistory / Psalmも保存目的が異なる。

## Decision

Recent Event Buffer、Incremental Statistics、Historical Milestones / Pins、Optional Raw Archiveを別層にする。高頻度Eventは日次・期間集計を基本とし、Birth、Death、Settlement lifecycle、Invasion、Conquest、Phase、重要人物・Pinは個別に長期保持できる。

StatisticsはEventまたは日末差分で増分更新し、rolling値は有限ringで保持する。UI、diagnostics、Completion判定は全raw historyを前提にしない。Raw ArchiveはCore外のoptional observerである。

## Consequences

- buffer容量、表示頻度、archive有無・圧縮を変えてもSimulation結果は変わらない。
- Event schemaはworld event、diagnostic aggregation、milestoneを識別できる必要がある。
- Memorable / Pinの具体閾値は未決のまま残す。

