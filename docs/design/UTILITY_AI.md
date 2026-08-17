# Utility AI

**Status:** Baseline boundaries / v0.2 default and configurable mechanics

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
after maturity (v0.15): Reproduction +0.04/day
successful Reproduction: both participants -6.0
```

受動的な会話参加者のCommunication Needは減らさない。ActivityとRestは完全な対称系にしない。将来は性格や必要人数等でNeed Weightや増加量を変えられる構造にする。

Move、Communication、Attack、Collision Attack、Flee、Reproduction Attemptは通常の能動Actionである。1 Micro Roundを消費した時点で、成功、失敗、miss、Rejectに関係なくActivity -2.0、Rest +0.5を適用する。

Restだけは専用のRest -4.0、Activity +1.0を使い、通常能動Actionの変化を重ねない。Counterattack、Pursuit Attack、Reproduction Accept / RejectはReactionであり、通常Action回数を消費せず、Activity / Restの通常変化も適用しない。

Communication Need -3は選択した側だけへ適用する。Reproduction Need -6とCooldown開始はReproduction Success時だけで、Reject時には適用しない。

v0.15でもRest Actionと既存Need変化は変更しない。Reproduction Needの日次増加だけを旧+0.01から+0.04へ変更する。

## Utility baseline

```text
U(action) = sum(Need_n * Effect(action, n))
          - (1 - RiskPreference) * Risk(action)
```

Needは `S=Survival`、`R=Rest`、`A=Activity`、`C=Communication`、`P=Reproduction`。Effectは原則 `[-1,1]` 程度で、正はNeedを満たす期待、負は悪化させる期待を表す。Riskは0〜10で状況自体の主観的危険度を表し、RiskPreferenceを含めない。

v0で遺伝可能な具体的Utility評価係数はRiskPreferenceだけ。0は強い危険回避、1は危険をほぼUtilityコストへ入れない。Loss Aversion、Future Discounting、Uncertainty Aversion、Other-regarding Preference等は将来Draftでありv0へ追加しない。

## v0 action utilities

次の係数はv0 default / configurableであり、Baselineではない。

```text
U_move          = 0.50 * A - 0.125 * R
U_rest          = 1.00 * R - 0.25 * A
U_communication = 0.75 * C + 0.50 * A - 0.125 * R
U_reproduce     = 1.00 * P + 0.50 * A - 0.40 * S - 0.20 * R
```

Order中のSettlement Core外ではv0.2 defaultとして `U_reproduce -= 2.0`、受諾側の `U_accept -= 2.0` を適用する。Generation中とSettlement Core内では適用しない。成功率へ別のrandom penaltyを加えず、野外Reproductionを禁止しない。

Move自体にRiskCostを置かず、方向はPerceptionに占有NPCが見えていてもseed付きランダムのままとする。Reality占有状態でMove Utilityや方向を変えず、Collision AttackはResolutionで発生させる。

Invasion ParticipantのMoveはv0.2でTarget Settlement CenterへのAdvance Biasを受ける。近傍のConceptMark HolderによるCohesion Biasは副とし、Advanceを上書きしない。これはMove方向のConfig / implementation detailであり、新Actionや別Utility式を追加しない。通常時のMove方向は従来通りseed付きランダムである。

CommunicationとReproductionに複数の有効対象がある場合、v0は対象をseed付きランダムで選ぶ。情報価値や相手の受諾見込みをUtilityへ入れず、Realityから先読みしない。

## Threat risk and subjective attack prediction

NPCは自身のCurrentHP、EffectiveMaxHP、Base/Effective能力、Needs、Ageを正確に把握してよい。他NPCのCombat、HP、Position等はPerception経由だけで使う。

```text
SelfHPRatio = CurrentHP_self / EffectiveMaxHP_self

R_threat(t) = Clamp(
  5 + 0.5 * (PerceivedCombat_t - EffectiveCombat_self)
    + 5 * (1 - SelfHPRatio),
  0, 10)

P_hit_subjective = Clamp(
  0.70 + 0.03 * (EffectiveCombat_self - PerceivedCombat_t),
  0.40, 0.95)

ExpectedDamage_subjective = max(
  1,
  4 + 0.9 * EffectiveCombat_self - 0.4 * PerceivedCombat_t)

ThreatNeutralization(t) = Clamp(
  P_hit_subjective * ExpectedDamage_subjective
  / max(PerceivedHP_t, 1),
  0, 1)

SurvivalPressure(t) = max(S, R_threat(t))

U_attack(t) = SurvivalPressure(t) * ThreatNeutralization(t)
            + 0.50 * A - 0.125 * R
            - (1 - RP) * R_threat(t)
```

ExpectedDamageではv0.15 Damage係数とReality側Damage乱数の期待倍率1.0を使う。無傷でSurvival Needが0でも、Threat RiskをSurvivalPressureへ含めることで明白な脅威へ反応できる。距離1以内の各PerceivedThreatについて個別のAttack Candidateと `U_attack(t)` を生成できる。

## Flee utility

距離3以内で `R_threat` 最大のPerceivedThreatをPrimaryThreatとし、同値はseed付き乱数で選ぶ。

```text
P_second = Clamp(0.02 * EffectiveAction, 0, 1)
ExpectedDistanceGain = 1 + P_second
FleeSafetyEffect = Clamp(ExpectedDistanceGain / 2, 0, 1)

R_pursuit = Clamp(
  5 + 0.5 * (PerceivedCombat_target - EffectiveCombat_self)
    + 5 * (1 - SelfHPRatio),
  0, 10)

U_flee = SurvivalPressure(PrimaryThreat) * FleeSafetyEffect
       + 0.50 * A - 0.125 * R
       - (1 - RP) * R_pursuit
```

Pursuit Reactionの `U_attack` は本節の対象別 `U_attack(t)` を参照する。

## Candidate choice

- 候補0件: Idle。Idleはこの場合だけ生成する。
- 候補1件: その候補で確定。
- 候補2件: 2件を抽選対象にする。
- 候補3件以上: Utility上位3件だけを残す。

有効Action Candidateが1件以上ある場合、Idleを通常候補へ加えない。

Reproduction Candidateの対象条件は主観的Alive、距離1、Matureだけとし、対象RealityのHP、Cooldown、実距離をDecisionへ渡さない。成立条件はResolutionが検証する。

Attack InterruptまたはReproduction Acceptで再評価した場合も同じUtility規則を使うが、新Intentは同一Micro Roundの終了済みphaseへ巻き戻して実行しない。再評価はAction枠の置換であり、追加Actionではない。

```text
weight_i = exp((utility_i - maxUtility) / temperature)
```

Configのtemperatureとseed付き乱数で重み付き選択する。最大Utilityを常に選ばず、通常はTop 3外を選ばない。候補の安定tie-breakを定義し、列挙順に依存させない。

## Capability and inheritance boundary

MaxHP、Action、Combat、Communicationは「何ができるか」、Utility評価係数は「結果をどう評価するか」である。基礎能力から自然に生じる行動傾向を同義の独立遺伝子として重複させない。

行動経済学・意思決定理論は将来の評価式設計に利用できるが、v0の具体式以外を採用済みとみなさない。

## Diagnostics and tests

候補、対象、Utility内訳、重み、選択結果、乱数purposeを診断可能にする。Reality非参照、同じNeed/Perception/seedの再現、候補0/1/2件、同点、temperature境界、Top 3外非選択、PerceivedCombat変更によるAttack/Flee Utility変化、Pursuitが同じ `U_attack` を使うことをheadless testで検証する。

採用理由は [`ADR-0001`](../decisions/ADR-0001-utility-ai.md) と [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。
