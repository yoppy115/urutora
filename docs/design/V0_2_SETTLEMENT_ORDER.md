# v0.2 Settlement / Order Update

**Status:** Baseline boundaries / v0.2.3 configurable default

本書はv0.15までの個体生態系へSettlement、Generation / Order、社会化、Invasion、Concept Auraを追加する。v0.15のUtility AI、Perception、Combat、Reproduction、Lifecycle、ConceptMarkは、本書が明示的に変更する範囲以外を維持する。

## Purpose

v0.15では旧v0の人口縮退を解消した一方、長期安定が大量出生、Collision由来Combat、Combat Deathの高回転で成立した。v0.2では秩序のない個体群が繁殖、移動、空間競合、Threat、Combatを繰り返す状態をWorld Lifecycleの**Generation（世界生成期 / 萌芽）**と解釈する。

Generation中、Reproduction Successが集中した場所からSettlementが自然発生する。人口動態が安定条件を満たした後にWorldPhaseをOrderへ移し、既存Settlementを局所的な社会秩序として有効化する。

## Settlement formation during Generation

SettlementはOrder開始を待たず、Generation中から順次生成できる。繁殖→滞在→Settlement形成→Affinity→所属という時間的連続性を保ち、過去のHotspotを後から遡及生成しない。

v0.2 defaultでは、直近90日間のReproduction Successを4×4 Cell windowで集計し、4件以上の領域をSettlement Candidateとする。15日ごとにCandidateを再評価する。既存Settlement CenterからChebyshev距離7以内へ新Centerを生成しない。

v0.2.1ではv0.2の実測2 Worldを補正根拠とし、同じ90日・15日評価の `HotspotSuccessThreshold` を3、`HotspotWindowSize` を5×5へ変更する。world-0001は385日でReproduction Success 152件、world-0002は290日で132件あったが、旧4×4評価日ごとの最大集中数はいずれも3で、Candidate評価・棄却・成立がすべて0件だった。5×5化は近接する繁殖成功を同一局所Hotspotとして扱う範囲変更であり、閾値3と組み合わせる。arbitration、Center、Founder、spacing、翌Tick反映は変更しない。

v0.2.3では既存Settlementの隣接エリアを当該SettlementのInfluenceと定義する。消滅していないSettlementのInfluence内で発生したReproduction Successは、新規Settlement用Hotspot集計から除外する。新規Center候補は、そのCenterを中心とするCore全Cellが既存SettlementのInfluenceと重ならないCellだけとする。同一evaluationで先に採用されたCenterにも同じ境界を適用する。消滅済みSettlementはHotspotとCenter候補を予約せず、その色と空間を後続Settlementが再利用できる。

同じevaluation日の全Candidateは、evaluation開始時の同一immutable snapshotから生成する。各CandidateはConfigで定めた領域（v0.2.1 defaultは5×5）、rolling window内Reproduction Success数、有効Center候補Cell、Founder候補を保持する。

排他距離で競合するCandidateはReproduction Success数の多い順、同数だけnamed seed streamで決定論的に優先する。v0.2.3 defaultでは従来のCenter spacing 7に加え、Influence radius 7とCore radius 2の非重複条件があるため、Center間Chebyshev距離は9より大きくなければならない。勝利CandidateのCenter確定後、この有効排他距離へ違反する残りCandidateを当該evaluationでは棄却し、競合しないCandidateへ同じ処理を続ける。Map走査、配列、Dictionary、thread scheduling順へ依存させない。棄却は永久ではなく、後日のevaluationで再Candidate化できる。

Candidate採用後、そのCandidate領域内でLandmarkではなく、かつ提案Coreが既存Influenceへ重ならないCenter候補Cellからseed付き乱数で1 Cell選ぶ。選択Centerが同日採用済みSettlementを含む排他条件へ違反した場合はCandidate全体を不成立とし、別Centerへ再抽選して迂回しない。Centerは物理占有物ではなく、NPCが侵入、滞在、占拠できる。

成立条件となったReproduction Success群の参加者のうち成立時点でAliveなNPCをFounderとして記録する。成立時に近くにいただけのNPCと区別し、History / Statisticsへ利用できる。

## Region and initial affinity

距離はSettlement CenterからのChebyshev距離で測る。

| Region | default | Role |
| --- | --- | --- |
| Core | v0.2–v0.2.2はradius 3、v0.2.3はradius 2（最大5×5） | Settlement内生活、Affinity、社会Bonusの主領域 |
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

両親が同じActive Settlement所属なら、受胎位置に関係なく子は通常の親近傍へ出生し、そのSettlementのMembershipThreshold相当AffinityとActive Affiliationを持って開始する。片親だけが所属する場合は、受胎時の両親がそのActive Settlement Influence内にいる場合に限り、子を同Influence内へ出生・所属させる。両親の所属が異なる場合は、受胎時に両者が同じ一意なActive Settlement Core内にいる従来条件だけを認め、子を同Core内へ出生・所属させる。出生解決時点で対象Settlementが非Activeなら通常の無所属出生へ戻す。親のAffinity数値を複製するものではない。詳細は [`REPRODUCTION.md`](REPRODUCTION.md) を正本とする。

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

## Settlement Maintenance and daily commit

Settlementの大きな構造変更は日中Micro Round途中でcommitせず、原則としてTick末のSettlement Maintenance Phaseへまとめる。

日中はAffinity発生要因、Settlement間CollisionによるFriction Event、Rest Collision、Invasion中Move / Combat、Invasion勝敗成立、Concept Aura等、その日のAction Resolutionに必要な処理を即時に扱う。征服統合はInvasion Victoryの結果であるため、自然消滅Maintenanceを待たず既存Invasion規則に従って処理できる。

Tick末Maintenanceは次の固定順とする。

1. 当日のWorld Event、Population、Birth、Death Statisticsを確定する。
2. 既存SettlementへのAffinity獲得を反映する。
3. Membershipと通常Affiliation変更を解決する。
4. Friction decayを処理する。
5. Population rolling windowとDemographic指標を更新する。
6. Reproduction Hotspot Candidateを生成する。
7. 同時Hotspot arbitrationを行い、新規Settlementをcommitする。
8. Settlement自然消滅条件を評価する。
9. Generation → Order条件を評価する。
10. 各SettlementのCrowdingPressureを更新する。
11. Invasion Eligibilityと新規Invasion開始を評価する。
12. 翌Tick用Settlement / World Phase Stateを確定する。

新規Settlement、新規World Phase、新規Invasion開始等の日末commitは、原則として翌Tickから通常Simulation Ruleへ反映する。同一日の途中で社会Ruleを切り替えず、処理順依存を防ぐ。

## Order settlement benefits

Order中だけ、Settlement Core内へ次を適用する。

- RestのRest Need減少効果を1.5倍する。既存の-4は-6となり、Activity側効果は変更しない。
- `DailyVitalChange > 0` は2.0倍する。
- `DailyVitalChange < 0` は負の絶対値を0.5倍する。
- Reproduction参加者2名が同一のActive Settlement Core内にいる場合だけSettlement内ReproductionとしてPenaltyを免除する。それ以外はv0.2 defaultで `U_reproduce -= 2.0`、`U_accept -= 2.0` とする。両者とも外、片方だけCore内、異なるCore扱い、Core境界をまたぐ場合はPenalty対象である。Membershipではなく、Actionが同じ社会空間内で行われるかを基準にする。成功率へ別の乱数Penaltyを直接加えず、野外繁殖を禁止しない。

Generation中は上記効果、Settlement治安Rule、Rest Collision Ruleを有効化しない。

## Order collision and local security

Order中のMove Collisionは関係により解決を変える。

- 同一Settlement所属者同士: Collision Attackへ変換せず、原則Combatを発生させない。
- Settlement Influence内のUnaffiliated NPC: Active PerceivedThreatでなければ、Settlement所属者はExplicit Attack Candidateを生成せず、Collisionも原則Combatへ変換しない。AttackIntent生成後もReality Resolutionで対象のUnaffiliated、Influence内、攻撃者にとってActive Threatでないという保護条件を再Validationし、古いIntentや同日State変化による迂回を防ぐ。
- 異なるSettlement所属者同士の平時Collision: Combatへ変換せず、Settlement間Frictionを増加させる。
- Invasion中の攻撃・防衛Settlement所属者同士: 敵対対象としてCombat可能で、CollisionもCombatへ移行できる。

Unaffiliated NPCがSettlement住民へAttack、Collision Attack等のThreat行為を行った場合、Settlement側のCounterattack、Threat Memory、Flee等の既存Reactionは通常通り有効である。対象をActive PerceivedThreatへ登録した後は、Threat Memory有効期間中、そのUnaffiliated NPCへのExplicit Attack Candidateも生成できる。Influenceは平時の無所属者を保護するが、実際のThreatへ既存Utility AIで対応できる領域である。

Settlement Centerからradius 5以内で、移動NPCが未実行Rest Intentを持つNPCへCollisionした場合、Rest Intentを解除する。解除されたNPCは元のRest Action枠を置換するため、同一Micro Round最大1回だけUtilityを再評価できる。追加Actionや無限再抽選を与えない。

## Friction and initial hostility

SettlementFrictionはSettlement Pair単位の対称な非負値とし、`Friction(A,B) = Friction(B,A)` である。方向性を持ち得るHostilityとは別概念にする。recordは少なくともSettlementAId、SettlementBId、CurrentFriction、LastFrictionEventTick、LifetimeFrictionEventsを保持可能にする。

v0.2 configurable defaultでは、平時の異Settlement Collisionで+1、平時の一方のSettlement所属NPCによる他Settlement所属NPCへのExplicit Attack等の直接Threat行為で+3する。Counterattackによって同じThreat Eventを二重加算しない。Active Invasion中の通常Combat一件ごとには加算せず、Invasion Eventを別のStatistics / Historyとして記録する。

Pairへ新しいFriction Eventが30日間なければ、以後30日ごとにCurrentFrictionを1減らし、0未満にしない。新EventはLastFrictionEventTickを更新してdecay待機期間を再開始する。

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

Advance Bias保持者が非死亡状態で自発的にEventから離脱する通常条件はRestだけである。Restを選ぶとAdvance BiasとInvasionParticipantを解除し、同一Invasionへ再参加させない。

Flee、Move、Attack、Communication、Reproduction等の通常ActionではParticipant状態を維持する。Fleeは一時的にThreatから距離を取るだけで、AliveかつAdvance Bias保持中なら後続Micro Round / TickでTarget Settlement方向へ再前進できる。Death、Event終了、Attack / Defense Victory、Settlement統合等でEvent自体が無効になった場合もParticipant状態を解除する。

防衛Settlement所属NPCには侵攻方向へCenterより前方に展開するDefense Biasを既存Moveへ加えられる。Defense Bias保持者がRestするとBiasだけを解除し、所属は維持して通常Settlement NPCへ戻る。

Invasion中は攻撃・防衛Settlement所属者を敵対対象としてCombat可能にする。

## Invasion victory and integration

Advance Biasを保持するAlive攻撃NPCが0になればDefense Victoryとする。次のどちらかでAttack Victoryとする。

- 対象Settlement Coreの利用可能Cellの50%以上を攻撃Settlement所属NPCが占拠。
- 対象Settlement Center Cellを攻撃Settlement所属NPCが占拠。

終了時にAdvance Bias、Defense Bias、Invasion Participant、所属変更Lockを解除する。

```text
CoreOccupationRate = AttackOccupiedUsableCoreCells
                   / TotalUsableCoreCells
```

`TotalUsableCoreCells` はCore内でNPCが物理的に占有可能なCell数とする。Map外とLandmark等の侵入不能Cellを除外し、Empty、防衛NPC占有、攻撃NPC占有、Settlement Center、その他通常移動可能なCellを含める。現在の占有状態ではなく本来利用可能かで分母を決める。`AttackOccupiedUsableCoreCells` は攻撃Settlement所属NPCが実際に占有する利用可能Core Cell数で、同一NPCを重複計上しない。50%以上でAttack Victoryとし、Center占拠の即時勝利も維持する。

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

同種Auraは複数Holderがいてもstackせず、異種Auraは併存できる。ConceptMark Holder本人には、自身と同じConcept種類のAura 1.1を追加適用せず、本人Mark 1.2を優先する。複数種類の本人Markは各対応能力へ同時に有効で、同種Mark自体はstackしない。範囲外へ出るとAuraは消失する。

生存Aura等の一時EffectiveMaxHP Bonus取得時はCurrentHPを増加させず、不足分を通常Vitality / Recoveryで回復可能にする。解除時にCurrentHPが新EffectiveMaxHPを超える場合だけ、新上限へClampする。このClampはAttack、Combat、Vitality Damageでも専用Damage Eventでもなく、ThreatやCombat Reactionを発生させないstate normalizationである。EffectiveMaxHP変化後のSurvivalNeedとHPRatioは次の通常State更新で新上限から再計算する。

Invasion ParticipantのHolderは、現在radius 2以内にいる同一Invasion参加中の味方へHolderへ近づくCohesion Biasを与える。遠距離から吸引しない。Advance Biasを主、Cohesion Biasを副とし、敵Centerへの前進を停止させるほど強くしない。複数Holderは最も近い者、同距離はseed付き乱数で選ぶ。

Settlement BonusはConceptMarkを直接付与しない。長寿化、定住、Combat Death減少がExposureを蓄積しやすくする間接作用だけを許し、Landmark→Individual Mark→Aura→Settlement Societyという最初の社会的伝播を作る。

## Observation application and diagnostics

Raw Logを人間が直接読み続けるより、ゲーム内Statistics UIでSimulation因果を確認できることを優先する。Desktop AppはMap上でSettlement Center、必要に応じてCore / Influence、Current World Phase、Settlement数、主要Statistics、Invasion中Settlementを識別可能にする。具体UIは実装裁量だが、表示やlogging量がSimulation結果を変えてはならない。

必須統計は [`STATISTICS.md`](../architecture/STATISTICS.md)、必須headless testsは [`TESTING.md`](../architecture/TESTING.md) を正本とする。

## v0.2.3 configurable defaults

次は初回Run用Configであり、不変のゲーム思想ではない。

| Area | Default |
| --- | --- |
| Hotspot | 90 days、5×5、Success 3、15-day evaluation。v0.2 originalは4×4 / Success 4 |
| Settlement spacing | Config最小Center distance > 7に加え、Coreと既存Influenceの非重複によりdefault実効値はCenter distance > 9 |
| Regions | Core radius 2（5×5）、Influence radius 7。既存Influence内のSuccessはHotspotから除外 |
| Initial Affinity | Founder +10、Core resident +7 |
| Affiliation | threshold 10、switch margin +5 |
| Core Affinity | Stay +0.05/day、Rest +1、Communication +0.5、Reproduction Success +2 |
| Settlement birth | 同一Active Settlement所属の両親は場所非依存で通常出生・同所属。片親所属は両親が所属先Influence内にいる場合に同Influence出生、異所属は一意なCore内条件で同Core出生。いずれもAffinity 10から開始 |
| Generation → Order | 90-day window、CV <= 0.10、Imbalance <= 0.20、30 consecutive days |
| Order benefits | Rest ×1.5、positive Vitality ×2、negative Vitality ×0.5 |
| Outside Core reproduction | 同一Active Settlement Core内の2名だけ免除。それ以外はU_reproduce -2、U_accept -2 |
| Rest Collision | radius 5 |
| Initial Hostility | 30% |
| Friction | Collision +1、Explicit Threat +3、30日無Event後は30日ごとに-1、minimum 0 |
| Crowding | 0.5 Occupancy + 0.5 BlockedMovement、threshold 0.70、30-day average for 30 days |
| Mobilization | 0.20 + 0.30 × Crowding、Clamp 0.20–0.50 |
| Natural dissolution | <= 10% World Population for 365 consecutive days |
| Exposure | radius 4、1/0.5/0.25/0.125、threshold 100 |
| Aura | radius 2、Rest -0.10/day、stat ×1.1 |

Advance BiasとAura Cohesionの具体Weightはv0.2 implementation/configurable detailである。Advanceを主、Cohesionを副とする制約を守る。

## Preserved v0.15 rules

C# / .NET、Core / App分離、1 Tick = 1日、64×64 Map、InitialPopulation 200、8方向Chebyshev、Top 3 + softmax、Micro Round、Targeted ActionのAttack → Reproduction → Communication順、Reality / Perception、Held Information、Communication、Threat Memory、Combat式、HP50 scale、Vitality Curve、MatureAge、Cooldown、遺伝、Mutation、Reproduction Need、ConceptMark本人1.2、seed determinismを維持する。

## Future direction preserved

一般のNPCをSettlementへ一律所属させない。同一Active Settlement所属の両親による場所非依存の出生所属、片親所属に対するInfluence内出生、異所属に対する一意なCore内出生だけを例外とし、それ以外は滞在、休息、交流、繁殖等のAffinity蓄積で所属する。回復、休息、長寿、繁殖、治安の強い利益により、結果として所属系統が長期的に有利となり、ほぼ全NPCが何らかのSettlementへ所属し得る社会化を目指す。

その成功が高密度、疾病、依存、階層、内部対立、資源不足、Settlement間戦争等の新Difficultyを生む方向性を維持する。v0.2はCrowdingからInvasionへ至る最初の接続だけを実装対象とし、他の将来制度を前倒ししない。

## Resolved v0.2 implementation boundaries

Settlement Maintenance順、同時Hotspot arbitration、Friction schema / 加算 / decay、Unaffiliated保護のCandidate / Resolution二重境界、Active Threat例外、同一Core Reproduction判定、本人Markと同種Aura、一時MaxHP normalization、Core占有率分母、Rest / FleeのInvasion離脱境界は確定済みである。

Advance BiasとAura Cohesionの具体Weight等、本文が明示的にimplementation / configurable detailへ委ねた値はConfig設計時に設定できるが、確定した主従・非stack・決定論境界を変更しない。

採用理由は [`ADR-0016`](../decisions/ADR-0016-generation-settlement-and-order.md)、[`ADR-0017`](../decisions/ADR-0017-settlement-conflict-and-invasion.md)、[`ADR-0018`](../decisions/ADR-0018-concept-aura-social-transmission.md) を参照する。
