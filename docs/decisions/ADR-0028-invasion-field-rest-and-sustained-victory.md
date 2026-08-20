# ADR-0028: model invasion field rest, retreat, and sustained victory

- **Status:** Accepted
- **Date:** 2026-08-18
- **Amends:** ADR-0017、ADR-0024
- **Amended by:** ADR-0030

## Context

旧InvasionはRest一回で同一Eventへ永久復帰不能となり、Usable Core 50%の瞬間占有で決着した。戦場での一時休息、重傷撤退、継続する前線、攻撃側の戦力崩壊を表現できなかった。

## Decision

ParticipantをAdvancing、Defending、FieldRest、Retreating、Deadへ分ける。通常Restは1日のFieldRest、HP比20%以下でのRest / Fleeは同一Eventへ戻らないRetreatingとする。ParticipantのMoveはCore / frontへ向かうInvasion Biasを最優先し、通常Utility AIとFleeを維持する。

Attack Victoryはusable Core 50%以上を3日連続とする。Defense VictoryはInitialAttackForce比30%以下を3日、Influence内Alive Non-Retreating attacker 0人を7日、またはAttack Victoryなしで90日経過のいずれかとする。

## Consequences

- 一度のRestによる永久離脱と瞬間50%占有Victoryは現行仕様ではない。
- FieldRestはAlive Non-Retreatingとして防衛条件へ数える。
- Event終了時にParticipant、Bias、Event専用Threat / lock、counterを明示的にcleanupする。
- 既存Damage、Hit、Friction、Mobilization、Alive-only integrationは維持し、Fission先行gateだけを追加する。
- ADR-0030により、攻撃者不在7日はCenter間距離に応じた連続日数へ、Mobilizationは2.0倍へ、armed / re-armは開始間隔60日へ置換された。Participant state、Core占領、戦力崩壊、90日膠着は維持する。
- ADR-0031により開始時は全Alive affiliated memberが参加し、同日のRestは除外しない。開始後のFieldRest / Retreating規則は維持する。
