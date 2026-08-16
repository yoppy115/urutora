# Perception and Held Information

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Reality boundary

RealityとNPCごとのPerceptionを別データ層にする。NPCはRealityを直接知れず、DecisionはPerceptionだけを使う。NPCは主観上可能なActionを選べるが、ActionIntentはReality側で失敗し得る。失敗もEventと直接経験になり、後のPerceptionへ影響できる。

```text
Reality -> Observation -> Perception
  -> PerceivedActionCandidate -> ActionIntent
  -> Reality validation / resolution -> ActionOutcome
  -> observable fact -> Perception update
```

## v0 information record

Perceptionは少なくとも次を保持可能にする。

- Subject
- Property
- EstimatedValue
- Confidence
- Source
- AcquiredBy: `Observation` または `Communication`
- AcquiredTick

主観には将来、記憶、噂、誤認、経験、関係、文化的解釈を加えられる。同じ事実を異なる意味として解釈できる上位正史を維持する。

## Held Information

Held Informationは、自分でObservationした情報とCommunicationで受け取った情報の集合だけである。Communicationで送れるのも送信者自身のHeld Informationだけで、知らないReality情報を生成してはならない。

Reality変更で既存情報を自動更新しない。古い情報を保持できる。同じSubject / Propertyの複数記録も保持可能で、Decision用代表値は次で選ぶ。

1. Confidenceが最も高い。
2. 同ConfidenceならAcquiredTickが新しい。

Observation由来の初期ConfidenceはCommunication由来より高くする。初期値と将来の減衰式はv0 Configであり普遍則ではない。

## Observation and immediate experience

日初Observationの最大Chebyshev距離は3。Micro Roundごとに周囲を完全再観測しない。本人が直接経験したAttack、失敗、情報受信、Pursuit、対象死亡等のActionOutcomeだけは即時更新できる。

## Threat Memory

Attack、Collision Attack、Pursuit Attackを直接受けたNPCは相手をPerceivedThreatとして記録する。これはPerception上の情報であり、明示的Attack Candidateは原則として距離1以内の既知Threatに対して生成する。

期限を持てる構造とし、v0 defaultは365日。再度脅威行為を受けるたび更新し、期間はConfig化する。これにより空間競合→最初の暴力→Threat Memory→継続的敵対という因果を作る。

## Player boundary

プレイヤーも通常はReality全知ではない。現在の表層状態を中心に観測し、完全な因果や全履歴の調査を主目的にしない。開発用debug projectionとは分ける。

## Minimum invariants

- 未観測Reality変更は、同一Perceptionとseedの候補順位・選択を変えない。
- Reality型をUtility評価器へ直接渡せない。
- CommunicationはHeld Information外の情報を生成しない。
- 数値変形とSubjectSwapは設定上限を超えず、未知Entityを生成しない。
- 隠れたReality差はActionOutcomeだけを変え得る。

採用理由は [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。

