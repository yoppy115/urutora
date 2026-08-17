# v0.2 Settlement / Order Update

**Status:** Baseline boundaries / v0.2 configurable defaults / explicitly unresolved details

本書はv0.15までの個体生態系へSettlement、Generation / Order、社会化、Invasion、Concept Auraを追加する。v0.15のUtility AI、Perception、Combat、Reproduction、Lifecycle、ConceptMarkは、本書が明示的に変更する範囲以外を維持する。

## Purpose

v0.15では旧v0の人口縮退を解消した一方、長期安定が大量出生、Collision由来Combat、Combat Deathの高回転で成立した。v0.2では秩序のない個体群が繁殖、移動、空間競合、Threat、Combatを繰り返す状態をWorld Lifecycleの**Generation（世界生成期 / 萌芽）**と解釈する。

Generation中、Reproduction Successが集中した場所からSettlementが自然発生する。人口動態が安定条件を満たした後にWorldPhaseをOrderへ移し、既存Settlementを局所的な社会秩序として有効化する。

## Settlement formation during Generation

SettlementはOrder開始を待たず、Generation中から順次生成できる。繁殖→滞在→Settlement形成→Affinity→所属という時間的連続性を保ち、過去のHotspotを後から遡及生成しない。

v0.2 defaultでは、直近90日間のReproduction Successを4×4 Cell windowで集計し、4件以上の領域をSettlement Candidateとする。15日ごとにCandidateを再評価する。既存Settlement CenterからChebyshev距離7以内へ新Centerを生成しない。

Candidate内の有効Cellからseed付き乱数でCenterを1 Cell選ぶ。Centerは物理占有物ではなく、NPCが侵入、滞在、占拠できる。

成立条件となったReproduction Success群の参加者のうち成立時点でAliveなNPCをFounderとして記録する。成立時に近くにいただけのNPCと区別し、History / Statisticsへ利用できる。

## Region and initial affinity

距離はSettlement CenterからのChebyshev距離で測る。

| Region | v0.2 default | Role |
| --- | --- | --- |
| Core | radius 3、最大7×7 | Settlement内生活、Affinity、社会Bonusの主領域 |
| Influence | radius 7、最大15×15 | 治安、所属形成、Settlement間空間関係 |

Settlement成立時のAffinityはFounderへ+10、Core内のAliveな非Founderへ+7。MembershipThresholdは10であるためFounderは原則即所属し、初期Core住民は少量の追加滞在・行動で所属できる。

## Affinity and affiliation

AffinityはGeneration中から有効で、Order前でも住民とSettlementの関係を形成する。Core内で次を加算する。

| Cause | v0.2 default |
| --- | --- |
| 1日滞在 | +0.05 |
| Rest | +1.0 |
| Communication | +0.5 |
| Reproduction Success | +2.0 |

1 NPCは原則1 Settlementへ所属する。Affinityが10以上になったSettlementへ所属できる。別Settlementへは `NewAffinity >= CurrentAffinity + 5` で移籍でき、頻繁な往復を抑制する。

Invasion参加中はEvent終了までActive Affiliationを固定する。Affinity履歴自体は更新可能だが、敵地の滞在や行動で戦闘中に所属を変更しない。征服後は統合規則を適用する。

## Generation and Order

世界開始時は `WorldPhase = Generation`。Generation中もSettlement生成、Founder、Affinity、所属、Frictionは存在できるが、Settlementの社会Bonusと秩序Ruleは有効にしない。

直近90日の人口系列について次を求める。

```text
PopulationCV = StandardDeviation(Population) / Mean(Population)

DemographicImbalance = abs(Births - Deaths)
                     / max(Births + Deaths, 1)
```

v0.2 defaultでは `PopulationCV <= 0.10` かつ `DemographicImbalance <= 0.20` を30日連続で満たすと、GenerationからOrderへ移行する。絶対人口や固定経過日数だけでは移行させない。

Order移行時に既存Settlementの社会秩序機能を解禁する。Order中に新規形成されたSettlementは成立時からOrder用Ruleを使える。

## Order settlement benefits

Order中だけ、Settlement Core内へ次を適用する。

- RestのRest Need減少効果を1.5倍する。既存の-4は-6となり、Activity側効果は変更しない。
- `DailyVitalChange > 0` は2.0倍する。
- `DailyVitalChange < 0` は負の絶対値を0.5倍する。
- Settlement Core外のReproductionは、v0.2 defaultで `U_reproduce -= 2.0`、`U_accept -= 2.0` とする。成功率へ別の乱数Penaltyを直接加えず、野外繁殖を禁止しない。

Generation中は上記効果、Settlement治安Rule、Rest Collision Ruleを有効化しない。

## Order collision and local security

Order中のMove Collisionは関係により解決を変える。

- 同一Settlement所属者同士: Collision Attackへ変換せず、原則Combatを発生させない。
- Settlement Influence内のUnaffiliated NPC: Settlement所属者から原則Attack対象にせず、Collisionも原則Combatへ変換しない。
- 異なるSettlement所属者同士の平時Collision: Combatへ変換せず、Settlement間Frictionを増加させる。
- Invasion中の攻撃・防衛Settlement所属者同士: 敵対対象としてCombat可能で、CollisionもCombatへ移行できる。

Unaffiliated NPCがSettlement住民へAttack、Collision Attack等のThreat行為を行った場合、Settlement側のCounterattack、Threat Memory、Flee等の既存Reactionは通常通り有効である。

Settlement Centerからradius 5以内で、移動NPCが未実行Rest Intentを持つNPCへCollisionした場合、Rest Intentを解除する。解除されたNPCは元のRest Action枠を置換するため、同一Micro Round最大1回だけUtilityを再評価できる。追加Actionや無限再抽選を与えない。

## Friction and initial hostility

Settlement A / B間に方向性または関係単位のFrictionを保持できる。平時の異Settlement Collision、Threat関係、Invasionに利用する。

新Settlement成立時、Founder cohortと成立時に即所属した初期住民について、既存Settlement B所属NPCをActive PerceivedThreatとして持つ割合をSettlement Bごとに調べる。30%以上なら `A -> B Initial Hostile` とする。Hostilityは片方向でよく、ランダム外交として生成しない。

これによりGenerationの個人Collision→Threat Memory→Settlement Formationが、社会関係へ変換される。

## Crowding and invasion eligibility

人口絶対値ではなく、実際の生活上の過密を測る。

```text
CrowdingPressure = Clamp(
  0.5 * CoreOccupancy
  + 0.5 * BlockedMovementRate,
  0, 1)
```

CoreOccupancyはCore内NPC占有率。BlockedMovementRateは所属NPCのFriendly Collision、Occupied CellによるMove Failure等から求める移動詰まり率である。

直近30日平均 `CrowdingPressure >= 0.70` が30日継続するとInvasion Eligibleになる。値はConfigである。

## Invasion target and mobilization

対象は次の優先順位で選ぶ。

1. Hostile Settlementを優先。
2. Hostile候補内でFriction最大。
3. Hostile候補がなければFriction最大。
4. 実質同値なら最寄り。
5. さらに同値ならseed付き乱数。

```text
MobilizationRate = Clamp(
  0.20 + 0.30 * CrowdingPressure,
  0.20, 0.50)

TargetForceSize = round(SettlementPopulation * MobilizationRate)
```

候補はAlive、攻撃Settlement所属、現在Rest中でない、他Invasionへ参加していないNPC。概ね半数をCore内からAffinity上位優先・同値seed付き乱数で選び、残りをCore外の所属NPCからseed付き乱数で選ぶ。一方が不足すれば他方から補充し、50/50は目標比率とする。

## Invasion movement and combat

参加者へ `InvasionParticipant = true` と対象SettlementへのAdvance Biasを与え、専用Actionを追加せず既存Moveの方向選択をTarget Centerへ近づくよう歪める。Utility AIとAttack、Flee、Communication、Rest、Reproduction等の候補は維持する。

Advance Bias保持者がRestを選ぶとBiasを解除しEventから離脱し、同一Invasionへ再参加させない。防衛Settlement所属NPCには侵攻方向へCenterより前方に展開するDefense Biasを既存Moveへ加えられ、Rest時に解除してよい。

Invasion中は攻撃・防衛Settlement所属者を敵対対象としてCombat可能にする。

## Invasion victory and integration

Advance Biasを保持するAlive攻撃NPCが0になればDefense Victoryとする。次のどちらかでAttack Victoryとする。

- 対象Settlement Core Cellの50%以上を攻撃Settlement所属NPCが占拠。
- 対象Settlement Center Cellを攻撃Settlement所属NPCが占拠。

終了時にAdvance Bias、Defense Bias、Invasion Participant、所属変更Lockを解除する。

攻撃側勝利では敗北Settlementを独立Settlementとして消滅させ、所属NPCのActive Affiliationを勝者へ統合し、敗北Centerを無効化する。旧AffinityはHistory / diagnosticsへ保存可能だがActive Membership判断には使わない。国家、属国、占領統治、反乱・忠誠はv0.2に含めない。

## Natural settlement dissolution

所属人口がWorld Populationの10%以下である状態が365日連続したSettlementは自然消滅可能とする。旧90日案は採用しない。

消滅後はActive Settlementと所属先としての資格を失い、残存所属NPCはUnaffiliatedへ戻す。旧Affinity、Founder、成立・消滅履歴はHistory / Statisticsへ保持できる。

## Concept exposure v0.2 change

Landmark ExposureをChebyshev radius 4へ拡大する。

| Distance | Daily exposure |
| --- | --- |
| 1 | +1.00 |
| 2 | +0.50 |
| 3 | +0.25 |
| 4 | +0.125 |
| 5以上 | 0 |

Threshold 100、ConceptMark本人の1.2倍、Base非変更、非遺伝は維持する。

## Concept Aura

ConceptMark HolderはChebyshev radius 2以内の同一Settlement所属NPCへ一時Auraを与える。敵とUnaffiliatedには作用しない。

共通効果はRest Need -0.10/day。Concept別のv0.2 defaultは次とする。

- 闘争Aura: EffectiveAction ×1.1、EffectiveCombat ×1.1。
- 生存Aura: EffectiveMaxHP ×1.1。
- 交流Aura: EffectiveCommunication ×1.1。

同種Auraは複数Holderがいてもstackせず、異種Auraは併存できる。ConceptMark本人の1.2効果とは別の一時効果だが、同種の無限stackを発生させない。範囲外へ出ると消失する。

Invasion ParticipantのHolderは、現在radius 2以内にいる同一Invasion参加中の味方へHolderへ近づくCohesion Biasを与える。遠距離から吸引しない。Advance Biasを主、Cohesion Biasを副とし、敵Centerへの前進を停止させるほど強くしない。複数Holderは最も近い者、同距離はseed付き乱数で選ぶ。

Settlement BonusはConceptMarkを直接付与しない。長寿化、定住、Combat Death減少がExposureを蓄積しやすくする間接作用だけを許し、Landmark→Individual Mark→Aura→Settlement Societyという最初の社会的伝播を作る。

## Observation application and diagnostics

Raw Logを人間が直接読み続けるより、ゲーム内Statistics UIでSimulation因果を確認できることを優先する。Desktop AppはMap上でSettlement Center、必要に応じてCore / Influence、Current World Phase、Settlement数、主要Statistics、Invasion中Settlementを識別可能にする。具体UIは実装裁量だが、表示やlogging量がSimulation結果を変えてはならない。

必須統計は [`STATISTICS.md`](../architecture/STATISTICS.md)、必須headless testsは [`TESTING.md`](../architecture/TESTING.md) を正本とする。

## v0.2 configurable defaults

次は初回Run用Configであり、不変のゲーム思想ではない。

| Area | Default |
| --- | --- |
| Hotspot | 90 days、4×4、Success 4、15-day evaluation |
| Settlement spacing | Center distance > 7 |
| Regions | Core radius 3、Influence radius 7 |
| Initial Affinity | Founder +10、Core resident +7 |
| Affiliation | threshold 10、switch margin +5 |
| Core Affinity | Stay +0.05/day、Rest +1、Communication +0.5、Reproduction Success +2 |
| Generation → Order | 90-day window、CV <= 0.10、Imbalance <= 0.20、30 consecutive days |
| Order benefits | Rest ×1.5、positive Vitality ×2、negative Vitality ×0.5 |
| Outside Core reproduction | U_reproduce -2、U_accept -2 |
| Rest Collision | radius 5 |
| Initial Hostility | 30% |
| Crowding | 0.5 Occupancy + 0.5 BlockedMovement、threshold 0.70、30-day average for 30 days |
| Mobilization | 0.20 + 0.30 × Crowding、Clamp 0.20–0.50 |
| Natural dissolution | <= 10% World Population for 365 consecutive days |
| Exposure | radius 4、1/0.5/0.25/0.125、threshold 100 |
| Aura | radius 2、Rest -0.10/day、stat ×1.1 |

Advance BiasとAura Cohesionの具体Weightはv0.2 implementation/configurable detailである。Advanceを主、Cohesionを副とする制約を守る。

## Preserved v0.15 rules

C# / .NET、Core / App分離、1 Tick = 1日、64×64 Map、InitialPopulation 200、8方向Chebyshev、Top 3 + softmax、Micro Round、Targeted ActionのAttack → Reproduction → Communication順、Reality / Perception、Held Information、Communication、Threat Memory、Combat式、HP50 scale、Vitality Curve、MatureAge、Cooldown、遺伝、Mutation、Reproduction Need、ConceptMark本人1.2、seed determinismを維持する。

## Future direction preserved

Settlementへの所属を規則で強制しない。回復、休息、長寿、繁殖、治安の強い利益により、結果として所属系統が長期的に有利となり、ほぼ全NPCが何らかのSettlementへ所属し得る社会化を目指す。

その成功が高密度、疾病、依存、階層、内部対立、資源不足、Settlement間戦争等の新Difficultyを生む方向性を維持する。v0.2はCrowdingからInvasionへ至る最初の接続だけを実装対象とし、他の将来制度を前倒ししない。

## Explicitly unresolved implementation decisions

以下は本文が値・規則を確定していないため、実装で独自確定しない。

- 同時に複数Hotspot Candidateが成立した場合のCandidate評価・Center生成の決定論的arbitration。
- Frictionの加算量、減衰、方向性または対称関係の具体schema。
- Settlement処理を既存日次tickのどの確定点へ挿入するかという詳細なcommit順。
- Unaffiliated NPCのThreat行為後、Counterattack以外の後続Explicit Attackを保護例外とするか。
- Unaffiliated保護をAction Candidate生成で扱うかReality Resolutionで扱うか、およびNPCへ他者Affiliationをどう認知させるか。
- Outside Reproduction PenaltyでいうCoreが「いずれかのCore」か「所属先Core」か。
- ConceptMark本人1.2と同種の他者Aura 1.1を同時に受ける場合の正確な合成規則、および生存Aura出入り時のCurrentHP上限処理。
- Attack VictoryのCore占有率で、Map外・Landmark等の利用不能Cellを分母へ含めるか。
- Flee等、Rest以外の行動がInvasion Participant / Advance Biasを解除する正確な条件。

採用理由は [`ADR-0016`](../decisions/ADR-0016-generation-settlement-and-order.md)、[`ADR-0017`](../decisions/ADR-0017-settlement-conflict-and-invasion.md)、[`ADR-0018`](../decisions/ADR-0018-concept-aura-social-transmission.md) を参照する。
