# ADR-0020: parallelize isolated read and planning phases with deterministic merge

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

人口300前後で1 Tickの待ち時間が増えた。主因には、各Observerが全Alive NPCを走査するObservationと、各NPCが独立に行う初回Intent planningがある。一方、Action Resolution、Event発行、Settlement Maintenanceをthread完了順へ委ねると、同一seedのEvent列と最終stateを壊す。

## Decision

- Observation対象は最大距離3のPosition索引から取得し、従来と同じSubject ID順で処理する。
- ObservationはObserverごとにHeld InformationとThreat Memoryの書込先を分離し、設定されたCPU並列度で処理できる。
- 初回Intent planningはNPCごとに分離し、用途別seed streamを維持したまま並列処理できる。
- DecisionTrace、diagnostic counter、IntentはNPC ID順にmergeする。
- Targeted Action、Movement、Reaction、Event発行、Tick末Maintenanceの権威的解決順は直列のまま維持する。
- `maximumDegreeOfParallelism = 0`は利用可能な論理CPU数、1は直列、2以上は上限指定とする。人口閾値未満は並列化しない。
- 固定seedについて直列設定と並列設定のEvent fingerprintおよび最終state fingerprint完全一致を回帰testにする。

## Consequences

- 多数NPC時に複数CPU coreを利用でき、近傍探索の計算量も抑えられる。
- CPU数とthread schedulingはSimulation結果へ影響しない。
- Resolutionは引き続き直列であり、人口増加に対して無制限に線形高速化するものではない。
- 並列度と開始人口はゲーム仕様ではなく、決定論を維持する交換可能な実行設定である。
