# ADR-0017: order lifts violence from individuals to settlements

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15のCollision Attackは最初の暴力とThreat Memoryを自然発生させたが、定住後も同じ個人衝突が無制限にCombatへ変換されると、Settlementが秩序装置にならない。一方、Combatを単純に消すだけでは生成期の主観的敵対が社会関係へ継承されない。

## Decision

Generationでは既存Collision Attackを基本とするが、ADR-0021により同一Active Settlement所属者間だけはProto-Orderとして抑制する。OrderではInfluence内Unaffiliatedへの保護と異Settlement間のFrictionを加える。Founder cohortと初期住民のPerceivedThreat比率から方向性を持つInitial Hostilityを生成する。

Frictionは対称Settlement Pairの非負値、Hostilityは方向性stateとして分離する。v0.2 defaultは異Settlement平時Collision +1、Explicit Threat Event +3で、Counterattackによる二重加算とInvasion中Combat一件ごとの加算をしない。30日Eventがなければ以後30日ごとに1減らす。

Influence内Unaffiliated非ThreatはCandidateとResolutionの両方でExplicit Attackから保護し、Active PerceivedThreatになった後は期限中のAttack Candidateを許可する。Outside Reproduction Penaltyは2名が同一Active Settlement Core内にいる場合だけ免除する。

実際のCrowdingPressureが継続したSettlementはInvasion Eligibleとなる。Hostility、Friction、距離から対象を選び、既存MoveへAdvance / Defense Biasを加える。専用Actionや別Utility AIは作らない。勝敗後はBiasとlockを解除し、攻撃側勝利では敗北Settlementを勝者へ統合する。v0.2.4の暫定guardrailはADR-0023を正本とする。

Core 50%勝利の分母はMap外・侵入不能Cellを除く利用可能Core Cellとする。侵攻側の通常の自発的離脱はRestだけで、FleeではParticipant / Advance Biasを維持する。Death、Event終了、Victory、統合ではstateを解除する。

## Reasons

- 個人Collision→Threat→社会間Friction / Hostilityという因果を保存する。
- 過密というSettlementの成功が次のDifficultyを生む。
- 既存ActionとUtilityを再利用し、Invasionだけの別Simulationを作らない。

## Consequences

- Spatial ResolutionはWorldPhase、Affiliation、Influence、Invasion関係を明示的に受け取る必要がある。
- Friction、Crowding、Invasion、統合を独立したdomain state / eventとして追跡する。
- 平時抑制と戦時Combatの条件をheadless testで固定する。
- Friction Event、保護拒否、利用可能Core占有率、離脱理由を構造化Event / Statisticsで分離する。
