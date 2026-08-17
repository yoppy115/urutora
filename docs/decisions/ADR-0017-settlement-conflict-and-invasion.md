# ADR-0017: order lifts violence from individuals to settlements

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15のCollision Attackは最初の暴力とThreat Memoryを自然発生させたが、定住後も同じ個人衝突が無制限にCombatへ変換されると、Settlementが秩序装置にならない。一方、Combatを単純に消すだけでは生成期の主観的敵対が社会関係へ継承されない。

## Decision

Generationでは既存Collision Attackを維持する。Orderでは同一SettlementとInfluence内UnaffiliatedへのCollision Combatを抑制し、異Settlement間の平時CollisionをFrictionへ変換する。Founder cohortと初期住民のPerceivedThreat比率から方向性を持つInitial Hostilityを生成する。

実際のCrowdingPressureが継続したSettlementはInvasion Eligibleとなる。Hostility、Friction、距離から対象を選び、既存MoveへAdvance / Defense Biasを加える。専用Actionや別Utility AIは作らない。勝敗後はBiasとlockを解除し、攻撃側勝利では敗北Settlementを勝者へ統合する。

## Reasons

- 個人Collision→Threat→社会間Friction / Hostilityという因果を保存する。
- 過密というSettlementの成功が次のDifficultyを生む。
- 既存ActionとUtilityを再利用し、Invasionだけの別Simulationを作らない。

## Consequences

- Spatial ResolutionはWorldPhase、Affiliation、Influence、Invasion関係を明示的に受け取る必要がある。
- Friction、Crowding、Invasion、統合を独立したdomain state / eventとして追跡する。
- 平時抑制と戦時Combatの条件をheadless testで固定する。
- Friction具体値、Hotspot arbitration、Core占有分母等の未決事項を実装で発明しない。
