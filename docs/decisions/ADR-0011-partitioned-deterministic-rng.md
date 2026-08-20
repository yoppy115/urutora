# ADR-0011: randomness is partitioned by deterministic purpose

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

単一共有乱数列では、無関係な抽選を一つ追加しただけで後続のUtility、戦闘、出生等が全面的に変化する。

## Decision

RunSeedから `subsystem / tick / entity / purpose` 等の安定keyで乱数streamを派生できる構造にする。UtilityChoice、Mutation、MoveDirection、MoveConflict、CommunicationDistortion、SubjectSwap、CombatHit、CombatDamage、BirthLocation等を用途分離する。列挙順、表示、ログ整形は乱数keyや消費順を変えない。

## Consequences

- 同じCode Version、Config、RunSeedから同じEvent列を再現する。
- stable ID、key encoding、PRNG algorithmを実装時に明示する。
- 無関係な乱数利用追加に対する局所性テストを用意する。
