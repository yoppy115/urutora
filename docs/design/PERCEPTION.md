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
- stable InformationId

主観には将来、記憶、噂、誤認、経験、関係、文化的解釈を加えられる。同じ事実を異なる意味として解釈できる上位正史を維持する。

## Held Information

Held Informationは、自分でObservationした情報とCommunicationで受け取った情報の集合だけである。Communicationで送れるのも送信者自身のHeld Informationだけで、知らないReality情報を生成してはならない。

Reality変更で既存情報を自動更新しない。古い情報を保持できる。同じSubject / Propertyの複数記録も保持可能で、Decision用代表値は次で選ぶ。

1. Confidenceが最も高い。
2. 同ConfidenceならAcquiredTickが新しい。
3. それも同じならseedに依存しないstable InformationId順。

Realityの最新値による自動上書きを禁止する。

v0.15では同一Subject + Propertyにつき最大3件だけHeld Informationとして保持できる。4件目以降のEviction Ruleは未決であり、最低Confidence、最古、複合score等のいずれも採用済みとみなさない。World Event LogはNPCの有限Memoryとは別で、長期保存できる。

## v0 direct observation

最大Chebyshev距離は3。直接Observationで最低限、Subject EntityId、Position、Alive、CurrentHP estimate、Combat estimate、ConceptMark有無、Perceived Life StageのChild / Matureを得られる。Action、Communication、RiskPreference、正確なAgeDays等の数値は直接取得しない。Old等の追加LifeStageはDraftである。

Subject EntityIdの取り違えはなく、PositionとAliveはObservation時点で正確に取得する。CurrentHP、Combat等の数値は距離 `d in {1,2,3}` に応じて次の誤差を持つ。

```text
ObservationErrorMax = 0.025 * (d + 1)
error = SeededRandom(-ObservationErrorMax, +ObservationErrorMax)
EstimatedValue = RealityValue * (1 + error)
```

最大誤差は距離1で±5%、距離2で±7.5%、距離3で±10%。v0 default Confidenceは距離1で1.00、距離2で0.90、距離3で0.80とする。誤差・ConfidenceはConfig化する。

## v0 communication confidence

受信者の品質を次で求め、送信元Confidenceをそのままコピーしない。

```text
CommQuality = Clamp(EffectiveCommunication_receiver, 0, 10)
TransmissionConfidenceFactor = 0.50 + 0.03 * CommQuality
ReceivedConfidence = Clamp(
  SourceInformationConfidence * TransmissionConfidenceFactor,
  0, 1)
```

Communication 0で係数0.50、10以上で0.80となり、伝聞を重ねるほど原則Confidenceが低下する。数値DistortionとSubjectSwapは別途適用する。直接ObservationはCommunication由来より原則高Confidenceである。

## Observation and immediate experience

日初Observationの最大Chebyshev距離は3。Micro Roundごとに周囲を完全再観測しない。本人が直接経験したAttack、失敗、情報受信、Pursuit、対象死亡等のActionOutcomeだけは即時更新できる。

Micro Round中にNPCが移動しても、他NPCのPosition認識は自動更新しない。Communication、Attack、Reproduction等は現在のPerceptionから候補を作り、対象がReality上ですでに距離外、死亡、不存在ならResolutionで失敗できる。そのOutcomeは本人へ即時反映できる。

Targeted ActionのResolution時に対象が既知Position / 距離に存在しなければTargetAbsentとなる。行動者はそのTargetのPositionをUnknownまたは無効Confidenceへ即時変更し、次Decisionで同じ古いPositionを根拠に同一Targeted Actionを作れないようにする。TargetAbsentだけから対象の死亡や正確な現在位置を推論・自動開示しない。

Attackされた、Attackが命中/失敗した、Communicationが成立/不成立だった、Reproductionが成立/不成立だった、Pursuitされた等の直接Outcomeも同日中に更新できる。ただしReproductionの非公開precondition値はOutcomeへ含めない。

v0ではNPCは自身のCurrentHP、EffectiveMaxHP、Base/Effective能力、Needs、Ageを正確に把握してよい。他NPCについてはPerceptionを必須とする。

## Threat Memory

Attack、Collision Attack、Pursuit Attackを直接受けたNPCは相手をPerceivedThreatとして記録する。これはPerception上の情報であり、明示的Attack Candidateは原則として距離1以内の既知Threatに対して生成する。

期限を持てる構造とし、v0.15 defaultは90日。再度脅威行為を受けるたび更新し、期間はConfig化する。これにより空間競合→最初の暴力→Threat Memory→継続的敵対という因果を作る。

直接Threat行為を受けるたびLastThreatTickを更新する。期限切れ後は明示的Attack/Flee Candidate対象から外すが、履歴ログまで削除する必要はない。Attackは距離1以内の各Threat、Fleeは距離3以内で最大 `R_threat` のPrimaryThreat、Pursuitは直前にFleeした対象だけを扱う。

Collision Attackの攻撃側はOutcomeから相手EntityId、相手Position、戦闘結果を直接知る。被攻撃側は攻撃者をPerceivedThreatへ登録する。

## Player boundary

プレイヤーも通常はReality全知ではない。現在の表層状態を中心に観測し、完全な因果や全履歴の調査を主目的にしない。開発用debug projectionとは分ける。

## Minimum invariants

- 未観測Reality変更は、同一Perceptionとseedの候補順位・選択を変えない。
- Reality型をUtility評価器へ直接渡せない。
- CommunicationはHeld Information外の情報を生成しない。
- 数値変形とSubjectSwapは設定上限を超えず、未知Entityを生成しない。
- Observation誤差は距離別上限を超えない。
- Communication受信Confidenceはsource Confidenceを上回らない。
- Held InformationはSubject + Propertyごとに3件を超えない。ただし未決のEviction結果をテストで固定しない。
- TargetAbsent後は同じ古いPositionをTargeted Action根拠へ使えない。
- 隠れたReality差はActionOutcomeだけを変え得る。

採用理由は [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。
