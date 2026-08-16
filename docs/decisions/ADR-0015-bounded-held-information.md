# ADR-0015: v0.15 bounds Held Information per subject property

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

初回RunではNPCのHeld Informationが長期的に無制限増加し得た。有限なNPC Memoryと長期保存可能なWorld Event Logを分離する必要がある。

## Decision

同一Subject + Propertyについて、NPCがHeld Informationとして保持できる記録を最大3件とする。Observation / Communication、Confidence、AcquiredTick等が異なる複数記録は保持できる。

World Event Logはこの容量制限の対象外である。

4件目を取得した場合は、Confidenceに関係なく最も先に取得した記録をFIFOで破棄する。ConfidenceはDecision代表値の選択にだけ使い、容量管理と情報評価を分ける。

Subjectの消滅を本人が直接ObservationまたはActionOutcomeで確認した場合、そのSubjectの全PropertyをHeld Informationから削除する。TargetAbsent、Position Unknown、死亡伝聞だけでは全削除しない。World Event / History Logは削除しない。

## Consequences

- capacity不変条件は確定する。
- Eviction結果が単純な取得順で決まり、Confidence調整がMemory容量管理を変えない。
- Subject消滅の直接確認と伝聞・不在を区別する必要がある。
