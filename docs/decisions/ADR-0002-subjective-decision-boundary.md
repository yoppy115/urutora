# ADR-0002: NPC decisions use subjective information and authoritative resolution

- **Status:** Accepted
- **Date:** 2026-08-16

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
- Communicationで扱える情報は、当事者がObservationまたはCommunicationで得たHeld Informationに限定する。

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

## Rejected alternatives

### Reality determines viable actions before Utility evaluation

NPCが知り得ない情報で候補が除外され、隠れた情報が意思決定へ漏れる。

### Perception directly mutates Reality

客観的な競合や成立条件を検証できない。
