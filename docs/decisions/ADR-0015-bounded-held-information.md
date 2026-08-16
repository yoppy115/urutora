# ADR-0015: v0.15 bounds Held Information per subject property

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

初回RunではNPCのHeld Informationが長期的に無制限増加し得た。有限なNPC Memoryと長期保存可能なWorld Event Logを分離する必要がある。

## Decision

同一Subject + Propertyについて、NPCがHeld Informationとして保持できる記録を最大3件とする。Observation / Communication、Confidence、AcquiredTick等が異なる複数記録は保持できる。

World Event Logはこの容量制限の対象外である。

## Unresolved

4件目以降のEviction Ruleは決めない。最低Confidence、最古、Confidence / Recency複合等を実装者が独自採用してはならない。

## Consequences

- capacity不変条件は確定する。
- どの記録が残るかを固定するテストと実装はEviction Rule確定まで保留する。
