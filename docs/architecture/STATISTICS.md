# v0.2.6 Statistics and Diagnostics

**Status:** Baseline observation obligations / Draft storage and UI mechanics

v0.2はRaw Logを人間が直接読み続けるより、ゲーム内Statistics UIとheadless集計で因果を確認できることを優先する。StatisticsはEvent / Realityのread-only projectionであり、集計や表示がSimulation結果、処理順、乱数消費を変えてはならない。

## World phase

最低限、次を確認可能にする。

- Current World Phase。
- Generation開始日、Order開始日。
- Population、PopulationCV。
- Births、Deaths、DemographicImbalance。
- Generation → Order条件の各現在値と連続成立日数。

Order Transitionの前90日と後90日をv0.2 default比較Windowとし、Population、Birth、Death、Combat Death、Collision Attack、Reproduction、平均Age、Settlement所属率を比較する。

## Settlement

World全体でSettlement数、Affiliated / Unaffiliated Population、所属率を持つ。各Settlementについて次を保持・表示可能にする。

- ID、Center、成立日、Founder数。
- 所属人口、World Population比。
- Affinity加入数、離脱数。
- Core Occupancy、UsableInfluenceCells、NominalResidentialCapacity、SettlementPressure。
- Initial Hostility、Friction。
- 消滅日、消滅理由、征服 / 統合先。
- Hotspot Candidate数、同時競合数、arbitration棄却数、Candidate別Reproduction Success数。
- Settlement Pair別CurrentFriction、Collision / root Threat raw件数、weighted件数、Living Population scale、daily impulse、decay前後、Invasion宣言時retention、Hostility。

headless統計とWorldログは消滅済みSettlementと全Friction履歴を保持する。v0.2.3 Desktop AppのWorld社会一覧は消滅済みSettlementを除外してActive / Pendingだけを表示し、Pair別FrictionとFriction変動Eventは選択したSettlementの詳細Tabへ限定して表示する。これは表示範囲だけの変更で、履歴projectionを削除しない。

## Affiliated versus unaffiliated

Settlement所属NPCとUnaffiliated NPCについて、最低限次を比較する。

- 平均Age、平均Death Age、平均HP。
- Combat Death数 / 率、Vitality Death数 / 率。
- Rest Action率。
- Reproduction Attempt、Success、Birth。
- ConceptMark取得数。

## Violence

最低限、次を分離集計する。

- Collision総数、Collision Attack数。
- 同Settlement Collision抑制数。
- Unaffiliated保護Collision数。
- Other Settlement Collision数、Friction増加件数。
- Explicit Attack、Counterattack、Pursuit Attack。
- Collision Attack / Explicit Attack / Counterattack / PursuitのDamage。
- Unaffiliated保護によるAttack Candidate抑制数、Resolution拒否数、Threat化後Explicit Attack数。

死亡は最後の一撃だけでなく、Combat Damage Sourceの寄与を診断可能な構造にする。

NPC詳細のキル数は、当該NPCが`Death` EventのTargetIdに記録され、DetailがCombat由来である件数とする。Vitality Deathや単なるAttack命中は含めない。

## Reproduction

Failureを少なくともTargetAbsent、Maturity、HP、Cooldown、Distance、Reject、Other Reality Failureへ可能な範囲で分離する。Settlement Core内 / 外でもAttempt、Success、Failureを比較可能にする。

同一Active Settlement Core内のAttempt / Successと、Outside Penalty対象のAttempt / Successを分離する。

NPC向けActionOutcomeへ非公開Reality値を露出しないという境界は維持する。診断projectionが見えることとNPCが知ることを混同しない。

## Invasion

各Invasion Eventについて次を確認可能にする。

- 開始日、終了日、Attack Settlement、Defense Settlement。
- Trigger SettlementPressure、HighPressureDays、cooldown残日数、前提別trigger rejection reason、Target選択理由。
- Alive affiliated population、Target / Actual Force Size、既存Invasion stateによる保留、Core / Frontier target・actual、最終participant ID。
- Advance Bias離脱数、Combat Death、Rest離脱。
- 最大Core占有率、Center占拠有無、Center hold days。
- Attack / Defense Victory、戦争期間、統合後人口。

## Concept and aura

最低限、次を集計する。

- Landmark別Exposure。
- ConceptMark取得数、現在Holder数、取得時Age。
- Settlement所属 / Unaffiliated別取得率。
- SettlementごとのMark Holder数。
- Aura対象延べNPC数、Invasion中Aura Holder数。
- Self Markと同種Auraの重複抑制数、生存Aura取得 / 解除数。

## Invasion occupation and withdrawal

- TotalUsableCoreCells、AttackOccupiedUsableCoreCells、CoreOccupationRate。
- Rest離脱、Death離脱、Flee中Participant数。

## Determinism boundary

- StatisticsはSimulation用random streamを消費しない。
- 表示、sampling、logging量を変えてもSimulation Event列を変えない。
- Event ID、Settlement ID、Invasion Event ID、Config、RunSeed、commitを用いてheadless結果を再照合できる。
- 集計値からSimulation Coreへcommandや補正を返さない。

保存形式、schema version、長期集計の圧縮、UIレイアウトはDraftである。

## v0.2.4 Settlement Stabilization

最低限、次をゲーム内Statisticsとheadless projectionから確認可能にする。

- Rest: Action率、選択時Rest Need / Pressure、Action別疲労寄与、平均Rest Need、所属別、Invasion参加者Restと離脱率。
- Home / residence: Settlementごとの総所属、Core / Influence / 外部人数と比率、Weak / Strong発動、Home方向Move、Core帰還、HP / RestNeed別Strong理由。
- Longevity: Formation、Natural Dissolution、Conquest Integration、Active数、平均 / 最大存続日数、LowSupport dissolution、Current Support、P/R/S、LowSupportDays。
- Ecology: 所属 / 無所属およびGeneration / Order別の人口、年齢、死亡年齢、HP、Birth、Reproduction、Combat / Vitality Death、Collision / Explicit Attack Damage。
- Foreign: Influence / Core進入、退出、Settlement間Collision、Friction Event、Current Friction。
- Proto-Order: 形成前後のCombat Death、同所属Collision抑制、HP、Positive Vitality Benefit、Affinity、Membership、Settlement survival。
- Invasion: 開始、最終攻撃開始tick、cooldown残日数、cooldown防止、Rest / Death離脱、最大Core占有率、Center Occupied、勝敗。

追加で次を必須とする。

- Pressure: Usable Influence Cells、Nominal Residential Capacity、30日Average Affiliated Population、ResidentLoad分子 / 分母 / 値。
- Congestion: Settlement Move Attempts、Blocked Settlement Move Events、block理由別件数、MovementCongestion。
- Return: Strong Home Move Attempts、Failed Strong Home Moves、failure理由、ReturnFailure。
- Trigger: SettlementPressure、HighPressureDays、LastInvasionStartedTick、cooldown残日数、Support / Phase / Active / target / participant等のrejection reason。
- Friction: 日次Pair raw Collision、root Explicit Threat、weighted events、Living Population A/B、scale、decay前後、impulse、retention前後、Hostility。
- Mobilization: Alive affiliated population、target / actual force、既存Invasion stateによる保留、Core / Frontier target / actual / fill、participant ID。
- Center: occupied、occupation start / end、hold days、non-victory reason、UsableCoreOccupationRate。
- Concept / Held Information: Exposure、Mark / Aura、同種抑制、Held Information総数、NPC平均 / 最大、FIFO eviction、直接purge、TargetAbsent Position invalidation。

v0.2.4までのSettlementSupport診断は90日windowの分子・分母も保持し、形成閾値の変更がReproduction Continuityへ反映されたことを確認できるようにする。v0.2.5ではこの瞬間値を`SupportPotential`と呼び、累積`SettlementSupport`と区別する。

## v0.2.5 Knowledge

Person Memory:

- NPCあたりPersonBelief平均・最大、平均capacity、capacity使用率。
- EverDirectlyObserved、HearsayOnly、ActiveThreat、同Settlement人物数。
- TTL削除、死亡認知削除、capacity削除、新record即時破棄。
- StableCommunication別の平均capacity。
- AggregatePersonConfidence平均、HearsayOnly / Confidence / Position Unknown順位別eviction。
- Active Threat、同Settlement、DirectObservedの保護数。

Knowledge fields:

- Unknown field率、Observation / Communication由来field数。
- 直接Observationが伝聞更新を拒否した数、SubjectSwap、死亡誤認後の再遭遇。
- NPCあたりEventBelief / SettlementBelief平均、EventType別保持、共有Event。
- Settlement ActiveStatus認知、Parent / Child、Hostility認知。
- Mandatory Memorable Event、Pin Importance 60以上、Importance 60未満非採用、EventType別EventBelief、共有Memorable Event。
- 自Affiliation、Center Observation、所属NPC Observation、Event参加、Communication別SettlementBelief作成・更新。
- Settlement Center / ActiveStatus / PopulationEstimate / Parent-Child Known率。

Communication:

- 送信総field、Event / Settlement / Person別件数と割合。
- Unknownを埋めた数、新record、既知field更新、矛盾情報。
- capacity不足で保存されなかったPerson field、平均sendCount、成功率。

## v0.2.5 Event layers

- Recent Event Buffer件数、Live Core保持Event件数、Incremental Statistics更新数。
- Historical Pin件数、Optional Archive出力件数、日次集計化したRaw Event件数。
- 全Event再scan回数と時間。通常UI更新では0を目標にする。

## v0.2.5 Support and fission

- SupportPotential、SettlementSupport、DailySupportDelta、SaturatedDays。
- RenewalCount、LastRenewalTick、LowSupportDays、Support 100到達日、Renewal間隔。
- Renewal後の人口・Pressure変化。
- Settlement別FissionPressureDays / eligible日数、hotspot candidate / 有効 / 不在数。
- Candidate Resident-Days、Cell Resident-Days最大、現在Alive NPC人口。
- Valid Center候補、現在居住Cell選択、中心距離 / seed tie、Valid Centerなし、次hotspot評価。
- Fission回数、migrant target / 実数、成立時即時 / Move後 / Flee後 / Tick末完了、migration中死亡、child無効化中断。
- 平均 / 最大Migration日数、Migration完了率。
- child成立、親人口変化、child人口、ParentChildNonAggression、FissionによるInvasion抑止。

## v0.2.5 Invasion

- Advancing、Defending、FieldRest、FieldRest復帰、Retreating。
- RestによるFieldRest / 重傷撤退、Fleeによる重傷撤退。
- InitialAttackForce、AliveNonRetreatingAttackParticipants、AttackForceRatio。
- AttackCollapseDays、InfluenceClearDays、AttackOccupationDays、UsableCoreOccupationRate。
- CenterDistance、InfluenceClearRequiredDays、LastInvasionStartedTick、CooldownDaysRemaining。
- CombatDeath、Retreat後生存率、Event期間、Victory Reason、親子target除外。

## v0.2.5 Population and expansion

- World / Affiliated / Unaffiliated Population、Settlement数。
- Parent / Child Settlement数、Fission系譜深度。
- 日次・年次人口増加率、Settlement別人口増加。
- 高Support・高Pressure Settlement数、Expansion候補状態日数。
- Fission Count、Invasion Count、Parent / Child network、Settlement間Friction・人口差。

これらを`Expansion Indicators`として扱う。Struggle Phaseの将来入力候補だが、v0.2.5ではphase transitionやRule / Bonusを発火させない。
