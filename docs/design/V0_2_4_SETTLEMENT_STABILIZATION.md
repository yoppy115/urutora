# v0.2.4 Settlement Stabilization Update

Status: **Baseline / v0.2.4 configurable defaults**

この文書は[`V0_2_SETTLEMENT_ORDER.md`](V0_2_SETTLEMENT_ORDER.md)を拡張し、明示した箇所だけを置換する。Reality / Perception、Utility、Targeted Action順、Combat、Reproduction、v0.15 Ecologyの未変更規則は維持する。

## Purpose

v0.2.3までにSettlement形成、所属者の生存優位、出生所属による世代継承を確認した。一方、過剰なRest、住民の非定住、形成と維持の断絶、小Settlementの反復消滅、無目的な他Settlement接近、同一Crowding episodeからのInvasion連打、Dead NPCの征服所属変更、Center一人到達による即征服、Frictionの無制限累積が観測された。

v0.2.4はSettlementの定住と維持を安定させ、残っていたInvasion Trigger、SettlementPressure、Friction、Mobilizationの実装境界を閉じる。国家制度や軍事占領の本格設計は後続版へ送る。

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

自然消滅ではActive Settlementから除外し、Aliveな所属NPCをUnaffiliatedへ戻す。Founder、History、Statisticsは保持でき、通常Mapでは非表示にできる。征服統合はこの判定を待たない。CoreOccupancyはSettlementPressureの入力には使わないが、状態観測、Invasion中のCore占有診断、将来システムのため維持する。

## Invasion stabilization

### Conquest affiliation

征服によるActive Affiliation変更はAlive NPCだけを対象とする。Dead NPCの最終所属Historyを書き換えず、征服理由の`AffiliationChanged` Eventも発生させない。

### SettlementPressure

旧`CrowdingPressure = 0.5 * CoreOccupancy + 0.5 * BlockedMovementRate`をInvasion Triggerから廃止し、所属人口負荷、移動混雑、帰還失敗を統合する`SettlementPressure`を使う。

`UsableInfluenceCells`はSettlement Influence内でNPCが物理的に占有可能なCell数である。Map外、Landmark、その他の侵入不能Cellを除外し、Empty、NPC occupied、Settlement Center、その他通常移動可能なCellを含める。

```text
NominalResidentialCapacity = max(
  1,
  floor(UsableInfluenceCells * 0.70))

ResidentLoad = Clamp(
  AverageAffiliatedPopulation30d
  / NominalResidentialCapacity,
  0, 1)
```

`AverageAffiliatedPopulation30d`は現在Influence内にいる人数ではなく、直近30日にAliveかつActive Affiliationを持つ全所属NPCの日次人口平均である。

```text
MovementCongestion = Clamp(
  BlockedSettlementMoveEvents30d
  / max(SettlementMoveAttempts30d, 1),
  0, 1)
```

分母は所属NPCが自Settlement Influence内で行ったMove attempt。分子は同所属NPCによる第一希望占有、代替を含む行先枯渇、Rest Collisionによる通行阻害、所属者占有によるfriendly collision suppressionを数える。Map境界、Landmark、その他静的障害だけによるblockは数えない。

```text
ReturnFailure = Clamp(
  FailedStrongHomeMoveAttempts30d
  / max(StrongHomeMoveAttempts30d, 1),
  0, 1)
```

Strong Home Bias Moveが自CoreへのChebyshev距離を減らさない、占有により有効な帰還方向を選べない、または有効行先がない場合を失敗とする。Flee、Invasion Advance / Defense等、Home Biasより高いpriorityを持つ移動は分母・分子から除外する。

```text
SettlementPressure = Clamp(
  0.45 * ResidentLoad
  + 0.35 * MovementCongestion
  + 0.20 * ReturnFailure,
  0, 1)
```

全成分は直近30日のrolling windowで集計し、Tick末Settlement Maintenanceで更新する。更新値は翌Tickから有効になる。`0.70`の容量比、各weight、windowはv0.2.4 configurable defaultである。

### Trigger and re-arm

新Invasionには次をすべて必要とする。

- World PhaseがOrder。
- 攻撃側SettlementがActive。
- `SettlementSupport >= 35`。
- 攻撃側がActive Invasionへ参加していない。
- `CrowdingInvasionArmed = true`。
- 攻撃可能な別Active Settlementが存在する。
- eligible participantが3名以上いる。

GenerationではInvasionを開始しない。`SettlementPressure >= 0.65`なら`HighPressureDays += 1`、下回れば0へresetする。30日連続し、上記前提を満たしたTick末にtargetとcohortを決めてInvasionを作成し、翌Tickから有効にする。

開始時に`CrowdingInvasionArmed = false`、`HighPressureDays = 0`、`LowPressureDays = 0`とする。Active Invasion中は新規Invasionを開始しない。Event終了後、Active Invasionがない状態で`SettlementPressure <= 0.45`なら`LowPressureDays += 1`、上回れば0へresetし、30日連続でre-armする。`0.45 < SettlementPressure < 0.65`ではHigh / Lowのどちらも進めない。

targetは既存priorityを維持する。Hostile候補を優先し、その中でFriction最大。Hostile候補がなければFriction最大、その後nearest、完全同値だけnamed seeded tie-breakで解決する。

### Mobilization

```text
MobilizationRate = Clamp(
  0.20 + 0.30 * SettlementPressure,
  0.20, 0.50)

TargetForceSize = floor(
  SettlementPopulation * MobilizationRate + 0.5)

ActualForceSize = min(TargetForceSize, EligibleParticipantCount)
```

`SettlementPopulation`は攻撃SettlementのAliveかつActive Affiliation人口。eligible participantはAlive、攻撃側へActive Affiliation、現在Rest中でない、別Invasionへ参加していないNPCである。`ActualForceSize < 3`ならInvasionを開始しない。

`CoreTarget = ceil(ActualForceSize / 2)`とし、Core内eligibleからAffinity降順、同値だけseeded tie-breakで選ぶ。残りは現在Core外のeligibleからseed付きrandomで選ぶ。一方が不足した場合は他方から補充する。Core / Frontier比は目標であり厳密制約ではない。RealityのCombat / Action値による全知的な採用優先は行わない。

### Friction

FrictionはSettlement Pairに対して対称、Hostilityは方向性を持つ別stateとし、Frictionは`0..100`へClampする。日ごと・Pairごとに、平時の異Settlement Collisionと、直接Attackをrootとする`ExplicitThreatIncident`を集計する。明示Attack、Threat Attack、平時Pursuit等を含むが、Counterattack等のReactionで同じroot incidentを二重計上しない。Active Invasion中の両陣営Combatは除外する。

```text
FrictionPairScale = max(
  10,
  sqrt(LivingPopulationA * LivingPopulationB))

WeightedFrictionEvents =
  PeacefulCrossSettlementCollisions
  + 4 * ExplicitThreatIncidents

DailyFrictionImpulse = Clamp(
  10 * WeightedFrictionEvents / FrictionPairScale,
  0, 5)

DecayedFriction =
  CurrentFriction * exp(-ln(2) / 180)

NewFriction = Clamp(
  DecayedFriction + DailyFrictionImpulse,
  0, 100)
```

`LivingPopulationA/B`は日末時点のAliveかつActive Affiliation人口。Invasion宣言時、対象PairのFrictionを`CurrentFriction * 0.25`へ低下させ、75%を消費した事実をEvent / Statisticsへ記録する。Hostilityは変えない。これは旧Collision `+1`、Threat `+3`のraw累積と「30日Eventなし後、30日ごとに-1」を置換する。

### Attack victory and Center

Settlement Centerにはv0.2.4の勝利規則を一切持たせない。到達、一時占有、複数日保持はいずれも勝利にしない。Attack VictoryはUsable Settlement Core Cellの50%以上を攻撃側Settlement NPCが占拠した場合だけ成立する。Center Occupied / Hold DaysはEvent / Statisticsへ記録できる。

### Departure

Advance ParticipantがRestするとAdvance BiasとParticipant状態を解除し、同じInvasion Eventへ再参加させない。後日の別Invasionには参加できる。Flee、Communication、Reproductionでは離脱しない。

## Unchanged systems

ConceptMark、Landmark、Aura、Held Informationは検討漏れではなく、v0.2.4で意図的に変更しない。Exposure radius 4と`1 / 0.5 / 0.25 / 0.125`、threshold 100、本人Mark`×1.2`、同Settlement Aura radius 2、Aura stat`×1.1`、同種非stack、Rest reduction、Invasion Cohesionを維持する。

Held InformationはSubject + Propertyごとに3件、FIFO eviction、代表値はConfidence優先・同値ならrecency、直接消滅確認時のみSubject全Property purge、TargetAbsentではPositionだけinvalid、World Event Logは別という既存境界を維持する。全Subject横断global cap、importance eviction、圧縮はv0.2.4へ入れない。

## Observation obligations

ゲーム内Statisticsから次を因果的に確認できることを要件とする。

- Rest率、選択時Rest Need / Pressure、Action別疲労寄与、所属別・Invasion参加者別Restと離脱。
- Settlementごとの総所属、Core / Influence / 外部人数と比率、Home Bias発動、帰還、発動理由。
- 形成、自然消滅、征服統合、Active数、存続日数、Support、P/R/S、LowSupportDays。
- 所属 / 無所属およびGeneration / Order別の人口、年齢、HP、出生、繁殖、死因、Damage。
- 他Influence / Coreへの進入・退出、Settlement間Collision、Friction。
- Proto-Order導入前後のCollision抑制、HP、Vitality Benefit、Affinity、Membership、Settlement survival。
- SettlementPressureの3成分、分子・分母・window、High / Low counter、armed、trigger rejection reason。
- Pair別Frictionのraw count、weighted count、population scale、decay前後、impulse、retention、Hostility。
- Mobilization target / actual / eligible / ineligible reason、Core / Frontier target / actual / fill、最終participant ID。
- Invasion数、armed / re-arm、防止数、離脱、同一Event再参加拒否、死者、最大Core占有率、Center Occupied / hold days、勝敗。
- Concept Exposure / Mark / AuraとHeld Information count / eviction / purgeが変更前境界を保っていること。

## Status

### Baseline additions

- Action種類別Rest fatigueと閾値付き対数Rest Pressure。
- Home Bias、自領域Move疲労軽減、平時のForeign avoidance。
- Generationの限定Proto-Order。
- 局所SettlementSupportとhysteresisによる自然消滅。
- SettlementPressure、hysteresis付きInvasion trigger / re-arm、正規化Friction、決定論的Mobilization。
- Aliveだけの征服所属、Rest離脱後の同一Event再参加禁止、Center非勝利、Core占領だけの勝利。
- Concept / Held Informationを変更しない境界。

### v0.2.4 configurable defaults

この文書に示した疲労量、Rest閾値・式係数、Home / Foreign weight、Generation倍率、Support window / weight / threshold、365 LowSupportDays、住宅容量比、Pressure weight / window / threshold / counter日数、Friction weight / scale floor / impulse cap / half-life / retention、Mobilization式 / cohort比、Friction上限、50%占拠条件は実験後に調整可能であり、不変の世界思想ではない。

### Deferred after v0.2.4

- Center保持日数、Center保持Victory、軍事占領。
- Rest離脱者の同一Invasion Event再参加。
- Concept数値の再調整。
- Held Informationの全体上限、importance eviction、圧縮。
- 軍事Leader、Combat / Action能力によるrecruitment優先。
- Active Invasion CombatによるFriction加算。

これらは現行挙動ではなくBacklogであり、実装裁量で確定しない。

