# Utility AI

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Decision pipeline

```text
Reality -> Observation -> Perception -> Needs
  -> PerceivedActionCandidate -> Utility evaluation
  -> top candidates -> weighted stochastic selection
  -> ActionIntent -> Reality-side resolution -> ActionOutcome
```

DecisionはPerception、自身のNeeds、自身が利用可能な内部状態だけを使う。Reality非公開値を読まず、「実際に勝てるか」ではなく「自分は勝てると思うか」を評価する。

## Needs baseline

Survival、Rest、Activity、Communication、Reproductionを0〜10へClampする。NeedはActionではなく、複数Needを同時に各ActionのUtilityへ寄与させる。最大Need一つだけでActionを決めない。

## v0 need defaults

```text
Survival = Clamp(10 * (1 - CurrentHP / EffectiveMaxHP), 0, 10)

daily: Activity +0.10, Rest +0.04
active Action: Activity -2.0, Rest +0.5
Rest Action: Rest -4.0, Activity +1.0

daily: Communication +0.05
initiated Communication: Communication -3.0

before maturity: Reproduction = 0
after maturity: Reproduction +0.01/day
successful Reproduction: both participants -6.0
```

受動的な会話参加者のCommunication Needは減らさない。ActivityとRestは完全な対称系にしない。将来は性格や必要人数等でNeed Weightや増加量を変えられる構造にする。

## Utility baseline

```text
Utility(action) =
  sum(Need_n * SubjectiveExpectedEffect(action, n))
  - RiskCost(action)
```

RiskCostへRiskPreferenceを作用させる。v0で遺伝可能な具体的Utility評価係数はRiskPreferenceだけ。0は危険回避的、1は危険をほぼ評価コストへ入れない。Loss Aversion、Future Discounting、Uncertainty Aversion、Other-regarding Preference等は将来Draftでありv0へ追加しない。

## Candidate choice

- 候補0件: Idle。
- 候補1件: その候補で確定。
- 候補2件: 2件を抽選対象にする。
- 候補3件以上: Utility上位3件だけを残す。

```text
weight_i = exp((utility_i - maxUtility) / temperature)
```

Configのtemperatureとseed付き乱数で重み付き選択する。最大Utilityを常に選ばず、通常はTop 3外を選ばない。候補の安定tie-breakを定義し、列挙順に依存させない。

## Capability and inheritance boundary

MaxHP、Action、Combat、Communicationは「何ができるか」、Utility評価係数は「結果をどう評価するか」である。基礎能力から自然に生じる行動傾向を同義の独立遺伝子として重複させない。

行動経済学・意思決定理論は将来の評価式設計に利用できるが、v0の具体式以外を採用済みとみなさない。

## Diagnostics and tests

候補、Utility、重み、選択結果、乱数purposeを診断可能にする。Reality非参照、同じPerceptionとseedの再現、候補0/1/2件、同点、temperature境界、Top 3外非選択をheadless testで検証する。

採用理由は [`ADR-0001`](../decisions/ADR-0001-utility-ai.md) と [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。

