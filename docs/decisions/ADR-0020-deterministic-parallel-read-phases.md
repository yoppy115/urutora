# ADR-0020: parallelize isolated read and planning phases with deterministic merge

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

多数NPCのObservationと初回Intent planningは並列化余地があるが、Resolution、Event、Maintenanceをthread完了順へ委ねると同一SeedのEvent列を壊す。

## Decision

- ObservationはPosition近傍indexを使って同じSubject ID順の意味論を維持する。
- Observerごと、NPC planningごとに書込先とrandom purposeを分離して並列化できる。
- DecisionTrace、counter、IntentはNPC ID順にmergeする。
- Targeted Action、Movement、Reaction、Event発行、Tick末Maintenanceの権威的解決順は直列のまま維持する。
- 並列度と開始人口閾値は実行設定であり、直列 / 並列のEvent・最終state fingerprint完全一致を回帰testにする。

## Consequences

CPU core数とthread schedulingはSimulation結果へ影響しない。Resolutionは直列のままで、無制限な高速化を意味しない。

