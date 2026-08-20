# ADR-0002: NPC decisions use subjective information and authoritative resolution

- **Status:** Accepted
- **Date:** 2026-08-16
- **Amended by:** ADR-0025

## Context

NPCが隠れたRealityを用いて行動を評価すると、誤認や情報差が意思決定へ作用しない。一方、主観だけでRealityを書き換えると、成立不能な行動を権威的に検証できない。

## Decision

Reality、Observation、Perception、Decision、Resolutionを分離する。

```text
PerceivedActionCandidate -> ActionIntent -> Reality resolution -> ActionOutcome
```

- Utility評価と候補生成はPerceptionだけを使う。
- NPCは主観上可能だと思う行動を選べる。
- Reality側はActionIntentを検証し、成功・失敗をActionOutcomeとして返す。
- ActionOutcomeは観測可能な事実を経由してPerceptionへ反映する。
- 日初以外の完全再観測は行わないが、本人が直接経験したOutcomeは即時にPerceptionへ反映できる。
- Communicationで扱える情報は、当事者がObservation、Communication、直接Outcome、Threat経験から得た主観Knowledgeに限定する。
- v0の直接ObservationはEntityId、Position、Alive、HP/Combat推定、ConceptMarkだけを取得し、数値推定へ距離依存誤差を入れる。
- Communicationはsource Confidenceへ受信者品質由来の減衰を掛け、Realityの最新値で主観履歴を自動上書きしない。
- NPCは自己のCurrentHP、EffectiveMaxHP、Base/Effective能力、Needs、Ageを正確に把握してよい。
- Reproduction Candidateは対象PerceptionのAlive / Position / Child-Matureだけを使い、対象RealityのHP / Cooldown / 実距離はResolutionで検証する。
- TargetAbsentは対象Positionを無効化するが、対象の死亡や現在位置を自動開示しない。
- v0.2.5では通常のfield優先規則を通って死亡認知が採用されたPersonBeliefを削除する。TargetAbsentは死亡を開示せず、Positionだけを無効化する。

## Reasons

- NPCごとの知識差と誤認を行動へ反映する。
- 客観状態の整合性をReality側で守る。
- 失敗そのものを新しい認識と逸脱の原因にする。

## Consequences

- Decision moduleはReality型へ依存できない構造が必要になる。
- 行動の主観的前提条件と客観的成立条件を分ける必要がある。
- 未観測Reality変更がUtilityを変えないことをテストする。
- ActionIntentとActionOutcomeに安定した識別子が必要になる。
- 古い主観情報のまま同日に複数行動することを許容する。
- Action対象が移動・死亡していればReality Resolutionで失敗させ、その直接Outcomeだけを即時反映する。
- NPC向けReproduction FailureへCooldown残日数等の非公開precondition値を含めない。
- PersonBelief capacity / TTL / evictionと死亡認知削除をEventBelief、SettlementBelief、World Event / History Logから分離する。

## Rejected alternatives

### Reality determines viable actions before Utility evaluation

NPCが知り得ない情報で候補が除外され、隠れた情報が意思決定へ漏れる。

### Perception directly mutates Reality

客観的な競合や成立条件を検証できない。
