# v0.2.4 Settlement Stabilization Update

Status: **Baseline / v0.2.4 configurable defaults**

この文書は[`V0_2_SETTLEMENT_ORDER.md`](V0_2_SETTLEMENT_ORDER.md)を拡張し、明示した箇所だけを置換する。Reality / Perception、Utility、Targeted Action順、Combat、Reproduction、v0.15 Ecologyの未変更規則は維持する。

## Purpose

v0.2.3までにSettlement形成、所属者の生存優位、出生所属による世代継承を確認した。一方、過剰なRest、住民の非定住、形成と維持の断絶、小Settlementの反復消滅、無目的な他Settlement接近、同一Crowding episodeからのInvasion連打、Dead NPCの征服所属変更、Center一人到達による即征服、Frictionの無制限累積が観測された。

v0.2.4はSettlementの定住と維持を安定させる。Invasion Trigger、Mobilization、Combatの本格再設計は後続版へ送る。

## Rest v2

通常能動Actionへ一律`Rest Need +0.5`を与える旧規則を廃止し、Actionごとの身体的疲労を使う。Rest Needは0～10のまま、新しい遺伝Traitは追加しない。

### v0.2.4 defaults

| Cause | Rest Need delta |
| --- | ---: |
| Daily passage | +0.02/day |
| Communication | +0.15 |
| Move | +0.25 |
| Reproduction Attempt | +0.35 |
| Attack | +0.60 |
| Collision Attack | +0.60 |
| Flee | +0.70 |
| Counterattack | +0.30 |
| Pursuit | +0.40 |

Collision AttackはMoveとAttackを二重加算せず`+0.60`だけを適用する。CounterattackとPursuitにも身体疲労を適用するが、Reactionへ通常Action用Activity変化やAction回数を追加しない。既存Activity Need規則は、本更新で明示した箇所以外を維持する。

Rest PressureはRest Needを`R`として次を使う。

```text
R <= 2:
  RestPressure = 0

R > 2:
  RestPressure = 10 * ln(1 + R - 2) / ln(9)

U_rest = RestPressure - 0.25 * ActivityNeed
```

通常Rest Actionの`Rest -4 / Activity +1`は維持する。Order Settlement Core内は既存Rest Bonusにより`Rest -6`。GenerationではこのOrder Bonusを使わない。

## Settlement movement

### Move fatigue reduction

Settlement所属NPCのMove疲労は、自Settlement Influence内で通常の`×0.75`（`+0.1875`）、Core内で`×0.50`（`+0.125`）。Settlement成立後はGenerationから有効。

### Home Bias

Home Biasは強制Actionではなく、通常Move候補のweightだけを変更する。

- 自Influence外でStrong条件を満たさない場合: Homeへ近づく`×1.5`、距離不変`×1.0`、離れる`×0.75`。
- `RestNeed >= 6`または`CurrentHP / EffectiveMaxHP <= 0.60`ならStrong Home Bias。Coreへ近づく`×5.0`、距離不変`×1.0`、離れる`×0.20`。
- Flee、Active InvasionのAdvance / Defense、その他緊急Actionを上書きしない。

### Foreign Settlement avoidance

平時の所属NPCは他Settlement Influenceへ入るMove候補を`×0.25`、Coreへ入る候補を`×0.05`にする。他Influence内に既にいる場合、外へ向かう候補を`×3.0`、深部へ向かう候補を`×0.25`にする。禁止ではない。攻撃対象へのActive InvasionとFleeでは適用せず、Unaffiliated NPCにも適用しない。

## Generation Proto-Order

Generation中のSettlementは次の限定機能を持つ。

- Settlement Formation、Founder、Affinity、Membership、Birth Affiliation。
- 同一Active Settlement所属者間のCollision Attack抑制。
- Core内の正の`DailyVitalChange ×1.25`。Order移行後は既存の`×2.0`。
- Stay、Rest、Communication、Reproduction Success等の通常Affinity gainを`×2.0`。形成時のFounder `+10`とInitial Core Resident `+7`は倍化しない。
- Home Bias、Move fatigue reduction、Foreign Settlement avoidance。

GenerationではOrder専用のRest `×1.5`、正Vitality `×2.0`、負Vitality / Aging `×0.5`、Outside Reproduction Penaltyをまだ有効にしない。Generationの正Vitality`×1.25`は負のVitalityへ適用しない。

## SettlementSupport and natural dissolution

World Population比10%以下が365日続く旧自然消滅条件を廃止し、Settlement自身の局所生活からSupportを算出する。

直近90日のrolling windowについて、各成分を0～1へClampする。

```text
P = Clamp(
      AverageAffiliatedResidentsInInfluence
      / FoundingResidentBaseline,
      0, 1)

FoundingResidentBaseline = max(
      FounderCount + InitialNonFounderCoreResidentCount,
      8)

R = Clamp(
      ReproductionSuccessInInfluence_90d
      / CurrentSettlementFormationReproductionThreshold,
      0, 1)

S = Clamp(
      SocialActionsInInfluence_90d
      / TargetSocialActions,
      0, 1)

TargetSocialActions = MemberDaysInInfluence * 0.25

SettlementSupport = 50 * P + 30 * R + 20 * S
```

`SocialActions`はInfluence内の所属NPCによるCommunicationとRest。Reproduction Continuityの分母は現行Hotspot形成閾値（v0.2.1 defaultは3）を再利用し、別閾値を作らない。

### Hysteresis

- `Support < 25`: `LowSupportDays += 1`。
- `25 <= Support < 35`: counterを凍結し、増加もresetもしない。
- `Support >= 35`: `LowSupportDays = 0`。
- `LowSupportDays >= 365`: 自然消滅。

自然消滅ではActive Settlementから除外し、Aliveな所属NPCをUnaffiliatedへ戻す。Founder、History、Statisticsは保持でき、通常Mapでは非表示にできる。征服統合はこの判定を待たない。CoreOccupancyはCrowding / Invasion診断のため暫定維持する。

## Invasion stabilization

### Conquest affiliation

征服によるActive Affiliation変更はAlive NPCだけを対象とする。Dead NPCの最終所属Historyを書き換えず、征服理由の`AffiliationChanged` Eventも発生させない。

### Crowding re-arm

Invasion開始時に`CrowdingInvasionArmed = false`とする。`CrowdingPressure < 0.70`が30日連続した後だけ再armし、その後あらためて既存eligibilityを満たさなければ新Invasionを開始できない。同一Crowding episodeから連打しない。

### Attack victory

Settlement Center Cellへ一人が到達するだけの即Attack Victoryを無効化する。v0.2.4の暫定条件はUsable Settlement Core Cellの50%以上を攻撃側Settlement NPCが占拠すること。Center OccupiedはEvent / Statisticsとして記録できるが勝利条件ではない。

### Friction and departure

Settlement Frictionを`[0,100]`へClampする。既存のCollision `+1`、Explicit Threat `+3`、既存decayは本版では再設計しない。Advance ParticipantがRestするとInvasionから離脱する規則も維持する。

## Unchanged systems

ConceptMark、Landmark、Aura、Held Informationは本版で変更しない。Exposure radius 4と`1 / 0.5 / 0.25 / 0.125`、本人Mark`×1.2`、同Settlement Aura radius 2、Aura stat`×1.1`、Rest reduction、Invasion Cohesionを維持する。

## Observation obligations

ゲーム内Statisticsから次を因果的に確認できることを要件とする。

- Rest率、選択時Rest Need / Pressure、Action別疲労寄与、所属別・Invasion参加者別Restと離脱。
- Settlementごとの総所属、Core / Influence / 外部人数と比率、Home Bias発動、帰還、発動理由。
- 形成、自然消滅、征服統合、Active数、存続日数、Support、P/R/S、LowSupportDays。
- 所属 / 無所属およびGeneration / Order別の人口、年齢、HP、出生、繁殖、死因、Damage。
- 他Influence / Coreへの進入・退出、Settlement間Collision、Friction。
- Proto-Order導入前後のCollision抑制、HP、Vitality Benefit、Affinity、Membership、Settlement survival。
- Invasion数、armed / re-arm、防止数、離脱、死者、最大Core占有率、Center Occupied、勝敗。

## Status

### Baseline additions

- Action種類別Rest fatigueと閾値付き対数Rest Pressure。
- Home Bias、自領域Move疲労軽減、平時のForeign avoidance。
- Generationの限定Proto-Order。
- 局所SettlementSupportとhysteresisによる自然消滅。
- Crowding episodeごとのre-arm、Aliveだけの征服所属、Center単独到達の非勝利。

### v0.2.4 configurable defaults

この文書に示した疲労量、Rest閾値・式係数、Home / Foreign weight、Generation倍率、Support window / weight / threshold、365 LowSupportDays、Crowding再arm条件、Friction上限、50%占拠条件は実験後に調整可能であり、不変の世界思想ではない。

### Deferred after v0.2.4

- Invasion TriggerとSettlementPressure。
- Crowding式。
- Friction生成・減衰の新モデル。
- Mobilization。
- Invasion Rest後の再参加。
- Center保持によるVictory。
- Conceptの再調整。
- Held Informationの再調整。

これらを本版の実装裁量で確定しない。

