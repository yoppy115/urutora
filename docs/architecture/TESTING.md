# v0 Headless Verification

**Status:** Baseline test obligations / Draft test implementation

`Simulation.Core.Tests` はGUIなしでCoreを高速実行する。失敗時にはCode Version、Config、RunSeed、tick、関係Entity ID、random purposeを再現可能な範囲で報告する。

## Required invariant groups

### Subjective decision boundary

- Decision層がReality型を直接読めない。
- 未観測Realityだけを変更しても、同じPerceptionとseedの候補順位・選択は変わらない。
- 同じPerceptionとseedから同じDecisionを得る。
- 同じNeed、Perception、seedから各Action Utilityを同値再現する。
- Attack UtilityがRealityのTarget Combat / HPを直接参照しない。
- PerceivedCombatを変えるとAttack / Flee Utilityが変化する。
- Pursuitの `U_attack` が通常Attackと同じ対象別定義を使う。

### Utility and scheduling

- 候補0件でIdle、1件で確定、2件で両方、3件以上でTop 3外を通常選択しない。
- 同点、負Utility、極端temperature等のedge caseが明示規則に従う。
- Action競合の結果がEntity配列順やDictionary列挙順に依存しない。
- 1日の最大Action数を超えない。

### Actions and reactions

- NPC占有CellへのMoveをCollision Attackへ変換し、同Actionで移動しない。
- CounterattackからCounterattackを再帰させない。
- Pursuit AttackからCounterattack、Flee、Pursuitを再帰させない。
- Communicationが送信者のPerson / Event / Settlement Knowledge外から情報を作らない。
- 数値distortionが設定上限を超えず、SubjectSwap率と置換候補境界を守る。
- Observation誤差が距離ごとの最大値を超えない。
- Communication受信Confidenceがsource Confidenceを上回らない。
- EffectiveCommunicationが10を超えてもdistortion率とSubjectSwap率が負にならない。
- 失敗した通常能動ActionにもActivity -2と、そのAction種別のv0.2.4 Rest fatigueを適用する。
- Reactionには通常Action用Activity変化を適用せず、Counterattack / Pursuitの身体疲労だけを適用する。

### Reproduction and lifecycle

- 非遺伝情報、Exposure、ConceptMarkが子へ渡らない。
- MutationとBirthLocationがseedで再現可能。
- Birth位置競合と空きCellなしの失敗が規則通り解決される。
- ConceptMarkがBase遺伝値を書き換えない。
- Vitality curve schemaが確定Life Phase形状と滑らかなcontrol-point接続を表現できる。Config初期値は、Phaseごとの符号・強弱、曲線の連続性、BaseMaxHP約50、1.5歳以降は自然回復なし、3歳前後からの強減衰、および不連続な大量死を起こさないという制約を満たす。
- Reproduction Reject時にReproduction NeedとCooldownを変えない。
- CurrentHP 0以下のNPCが後続Micro RoundやReactionへ参加しない。
- 死亡Cellを後続Micro RoundとBirth解決で利用できる。
- 複数BirthRequest競合の結果がqueue順変更で変わらない。

### Whole-run determinism

- 同じCode Version、Config、RunSeedから同じSimulation Event列を得る。
- render頻度、frame rate、Event Log表示有無を変えてもEvent列が変わらない。
- 無関係なpurposeの乱数利用追加が、既存purposeのstreamをずらさない。

### v0.15 ecology update

- Targeted ActionをMove / Flee / Restより先に解決する。
- AttackされたNPCのIntent再評価は同一Micro Round最大1回で、複数Attackでも増えない。
- Reproduction Rejectは相手の既存Intentを維持し、Acceptは最大1回だけ置換する。
- TargetAbsent後、同じ古いPositionを使うTargeted Actionを反復しない。
- Reproduction Candidateが対象RealityのCooldown / HPを読まず、ResolutionがHP / Cooldown / Distanceを検証する。
- v0.15時点のSubject + Property 3件FIFOは履歴回帰として隔離し、現行v0.2.5ではPersonBelief capacity / TTL / evictionを検証する。
- MatureAge 180日、ReproductionCooldown 90日、ThreatMemoryDuration 90日、ReproductionNeedGain +0.04/dayをConfig defaultとして検証する。
- BaseMaxHP約50 scaleと新Damage係数 `4 + 0.9*AttackCombat - 0.4*DefenseCombat`、Random(0.9,1.1)を検証する。
- Hit Rate、Counterattack構造、Concept Exposure / Mark値がv0.15で変化していないことを回帰検証する。
- InitialAgeが180〜700日のConfig範囲からseed付き生成される。

### v0.15 resolved patch

- 旧4件目FIFOはv0.15互換fixtureに限定し、現行PersonBeliefへ適用しない。
- v0.15の直接死亡確認purgeは履歴として保持し、現行では採用済みDeath fieldによるrecord削除を検証する。
- TargetAbsentではPositionだけを無効化し、Subject全体を削除しない。
- Communicationによる死亡伝聞はfield優先規則を通し、低優先なら削除せず、採用されたDeadならPersonBeliefを削除する。
- 同一Micro RoundのTargeted ActionをAttack → Reproduction → Communication順で解決する。
- Attackで死亡したTargetへの後続Reproduction / Communicationを成立させない。
- Attack後にHP条件が崩れたReproductionをReality Validationで失敗させる。
- Interrupt再抽選Attackを終了済みAttack Phaseへ巻き戻して実行しない。
- Vitality Configが0〜0.5歳の回復力上昇、0.5〜1歳の強回復、1〜1.5歳の回復低下、1.5〜2.5歳の弱減衰、2.5〜3歳の減衰加速、3歳以降の強減衰を満たす。

### v0.2 Settlement / Order Update

- A. Generation中でもReproduction HotspotからSettlementが生成される。
- B. v0.2 originalではGeneration中の社会Bonusが無効だったことを履歴として保持する。v0.2.4回帰では限定Proto-Orderだけを有効にする。
- C. PopulationCVとDemographicImbalanceの安定条件が連続成立した後にOrderへ移行する。
- D. Order移行後、既存Settlementの社会Bonusが有効になる。
- E. Settlement成立原因となったReproduction Success参加者のうちAliveなNPCをFounderとして記録する。
- F. 成立時にFounder +10、Core内非Founder +7の初期Affinity defaultを付与する。
- G. AffinityがMembershipThresholdへ達すると所属が成立する。
- H. 通常所属変更がCurrent Affinityとの差+5 defaultに従う。
- I. Invasion参加中はActive Affiliationを変更しない。
- J. Order中の同Settlement CollisionをCombatへ変換しない。
- K. Settlement Influence内のUnaffiliated NPCをSettlement側から通常Attackせず、Collision Combatを抑制する。
- L. Unaffiliated NPCがThreat行為を行った場合、Settlement側Counterattack / Threat Memoryが有効である。
- M. Center radius 5内のRest CollisionでRest Intentを解除し、元Action枠の再評価を同一Micro Round最大1回に制限する。
- N. 異Settlement平時CollisionをCombatではなくFrictionへ変換する。
- O. SettlementPressureをResidentLoad / MovementCongestion / ReturnFailureから算出し、CoreOccupancyを入力に使わない。
- P. SettlementPressure 0.65以上の30日継続だけでは開始せず、v0.2.5のFissionPressure 90日とhotspotなしを追加要求する。
- Q. Invasion対象がHostility、Friction、Distance、seed tie-breakの優先順位に従う。
- R. MobilizationRateがSettlementPressureに応じ20〜50%で変化する。
- S. Core CohortをCore内のAffinity上位から選び、同値をseed付きで解決する。
- T. Frontier CohortをCore外の所属NPCから選ぶ。
- U. Rest中NPCをInvasion参加候補から除外する。
- V. 非重傷ParticipantのRestは1日FieldRest、重傷Rest / FleeはRetreatingとなる。
- W. Defense Victoryは攻撃軍比、Influence排除、90日膠着の継続条件に従う。
- X. Core 50%以上を3日連続占拠するとAttack Victoryになる。Center単独占拠は勝利条件ではない。
- Y. 攻撃側勝利後、敗北Settlementを無効化してAliveな所属NPCだけを勝者へ統合する。
- Z. World Population比による旧自然消滅条件はv0.2 originalの履歴とし、v0.2.4 Support条件へ置換する。
- AA. Landmark Exposureをradius 4まで付与し、距離4でv0.2 default +0.125となる。
- AB. Concept Auraが同Settlement所属の味方だけに作用し、敵とUnaffiliatedへ作用しない。
- AC. Aura RangeがChebyshev radius 2である。
- AD. 同種Auraを複数Holderから受けてもstackしない。
- AE. Invasion中、radius 2以内の同一Event参加者へConceptMark Holder方向のCohesion Biasが発生する。
- AF. Aura CohesionがEnemy SettlementへのAdvance Biasを完全に上書きしない。

加えて、Settlement生成、cohort選択、target tie-break、Friction / Invasion Event、Aura対象がcollection列挙順やUI表示頻度に依存しないことを検証する。

### v0.2 resolved-boundary patch

- Patch A. 同日複数Hotspotの競合ではReproduction Success数が多いCandidateを優先する。
- Patch B. 同数Candidateのseed tie-breakを決定論的に再現する。
- Patch C. Map scan、collection、thread順を変えてもHotspot arbitration結果が変わらない。
- Patch D. 異Settlement平時Collisionを日次Friction raw countへ追加する。
- Patch E. 平時root Explicit Threat Incidentへweight 4を適用する。
- Patch F. Counterattackで同一Threat EventのFrictionを二重加算しない。
- Patch G. Frictionへ半減期180日の連続日次decayを適用し0未満にしない。
- Patch H. Influence内Unaffiliated非ThreatへSettlement NPCがExplicit Attack Candidateを生成しない。
- Patch I. UnaffiliatedがActive ThreatになるとExplicit Attack Candidateを生成でき、Resolutionでも最新保護条件を再検証する。
- Patch J. Reproduction参加者2名が同一Active Settlement Core内でない場合、Outside Penaltyを適用する。
- Patch K. ConceptMark Holder本人へ同種Aura 1.1を追加適用しない。
- Patch L. 生存Aura取得時にCurrentHPを自動増加させない。
- Patch M. 生存Aura解除時、CurrentHPが新EffectiveMaxHPを超える場合だけClampする。
- Patch N. Aura解除ClampをCombat / Vitality Damage Eventとして扱わず、Reactionを発生させない。
- Patch O. Core占有率分母からMap外とLandmark等の侵入不能Cellを除外する。
- Patch P. 防衛NPC占有Cellを利用可能Core Cellとして分母へ含める。
- Patch Q. HP比20%超のFleeはParticipantを維持し、20%以下ではRetreatingとなる。
- Patch R. 非重傷RestはFieldRestから復帰し、HP比20%以下のRestだけをRetreatingとして同一Eventへ戻さない。
- Patch S. Death、Event終了、Victory、統合時にParticipant状態を解除する。

Settlement Maintenance順と翌Tick反映、Aura / Core占有計算もcollection order、scan order、thread schedulingに依存しないことを検証する。

### v0.2.1 Settlement Hotspot regression

- v0.2のclean logを生成したseed 8147291と8147292をv0.2.1 defaultで再実行し、Settlement CandidateとActive Settlementが生じる。
- defaultは90日、5×5、15日評価、spacing 7、`HotspotSuccessThreshold = 3` とする。
- 同一seed、Config、tick数のEvent列と最終stateは引き続きreplay一致する。

### Settlement birth and observation regression

- 両親が同じActive Settlement所属なら、Core外でも通常の親近傍へ出生し、同SettlementのMembershipThresholdから開始する。
- 片親所属は、受胎時に両者が所属先のActive Influence内にいる場合だけ同Influenceへ出生・所属する。
- Influence境界をまたぐ片親所属の繁殖は出生所属を付与しない。
- 異所属は、受胎時に両者が同じ一意なActive Core内の場合だけ同Coreへ出生・所属する。
- BirthRequestの新しい社会stateを含めても、Birth arbitration、Event列、最終stateの決定論を維持する。
- ConceptMark所持者のMap描画にConcept固有色の旗pixelが存在し、Settlement所属輪郭と別レイヤーになる。
- UI smokeで速度段階が1 / 2 / 3 / 5 / 10 / 50日の順に構成される。
- 同一TickのWorld Statistics queryは同じread-only projectionを再利用し、次のauthoritative advanceで無効化する。

### v0.2.3 Settlement boundary, details, and parallel regression

- Config defaultはCore radius 2（5×5）で、既存Settlement Influence内のReproduction Successを新規Hotspotへ含めない。
- 新規Center候補の5×5 Coreは既存Settlement Influenceと重ならず、消滅済みSettlementは空間を予約しない。
- `maximumDegreeOfParallelism = 1`と4で、固定seedのEvent fingerprint列と最終state fingerprintが完全一致する。
- NPCキル数はCombat由来Deathだけを数え、Vitality Deathや非致死Attackを含めない。
- Settlement CenterとNPCが同じCellにいる場合、Center clickを優先してSettlement詳細を開く。
- Settlement色は60色をActive中に固定し、消滅後に解放された色を新規Settlementの抽選候補へ戻す。
- World統計の社会一覧は消滅済みSettlementを除外してActive / Pendingだけを表示し、FrictionはSettlement詳細Tabだけへ表示する。
### v0.2.1–v0.2.3 adopted minors

- Hotspotは90日、5×5、Success 3、15日評価で、旧4×4 / Success 4より成立可能になる。
- 既存Active Settlement Influence内のSuccessをHotspotから除外する。
- Success発生時点で参加者の一方でもActive Settlement所属なら、Influence外でも新規Hotspotから除外する。
- 新Core全Cellが既存Influenceへ重ならず、defaultではCenter距離`> 9`となる。
- 同じActive Settlement所属の両親は位置に依存せず出生所属を継承する。
- 片親所属は受胎時に両親とも所属先Influence内の場合だけInfluence出生所属となる。
- 異所属は両親が同じ一意なActive Core内の場合だけCore出生所属となる。
- Observation cache、NPC近傍index、CPU並列化、UI速度・描画頻度・CPU core数でEvent列が変化しない。
- 消滅Settlement非表示、色再利用、Settlement詳細、Friction表示、NPC履歴がCore stateを変更しない。

### v0.2.4 Settlement Stabilization

- A. ActionごとのRest fatigueがv0.2.4値に従う。
- B. Collision AttackでMove + Attackの二重疲労が発生しない。
- C. Daily Rest増加が`+0.02`。
- D. `RestNeed <= 2`で`RestPressure = 0`。
- E. RestPressureが規定の対数式に従う。
- F. `U_rest = RestPressure - 0.25 * ActivityNeed`。
- G. 自Settlement Influence内Move疲労が通常の75%。
- H. 自Settlement Core内Move疲労が通常の50%。
- I. Influence外所属NPCにWeak Home Biasが発生する。
- J. `RestNeed >= 6`でStrong Home Biasが発生する。
- K. HP ratio `<= 0.60`でStrong Home Biasが発生する。
- L. Foreign Influence進入Move weightが`×0.25`。
- M. Foreign Core進入Move weightが`×0.05`。
- N. Foreign Settlement内部から退出方向が`×3`。
- O. Active Invasion / Flee時にForeign avoidanceが不当に優先されない。
- O2. Active Invasion参加者は攻撃・防衛ともHome / Foreign Biasを受けず、攻撃側は敵Core Centerへの接近`×5` / 不変`×1` / 離脱`×0.2`となる。
- P. Generation中でも同Settlement Collision Attackを抑制する。
- Q. Generation Coreの正Vitalityが`×1.25`。
- R. Generationの通常Affinity gainが`×2`。
- S. Founder `+10` / Initial Core `+7`をGeneration multiplierで二重化しない。
- T. Order移行後はGeneration`×1.25`でなくOrder正Vitality`×2`になる。
- U. v0.2.4の`50P + 30R + 20S`をv0.2.5では`SupportPotential`として算出し、別stateの累積Supportを更新する。
- V. FoundingResidentBaselineが成立時人数・最低8に従う。
- W. Reproduction Continuityが現行Formation thresholdを再利用する。
- X. `Support < 25`でLowSupportDaysが増える。
- Y. `25 <= Support < 35`でLowSupportDaysがfreezeする。
- Z. `Support >= 35`でLowSupportDaysがresetする。
- AA. LowSupportDays 365で自然消滅する。
- AB. World Population比だけでSettlementが消滅しない。
- AC. ConquestでDead NPCのAffiliationとHistoryを変更しない。
- AD. Invasion開始時に`CrowdingInvasionArmed = false`となる。
- AE. Active Invasion終了後、SettlementPressure`<= 0.45`が30日連続するまでre-armしない。
- AF. Center Cell占拠だけではAttack Victoryにならない。
- AG. Usable Core 50%以上を3日連続占拠するとAttack Victoryになる。
- AH. Frictionを0～100へClampする。
- AI. Rest v2を維持しつつ、非重傷Invasion RestはFieldRest、重傷時だけRetreatingとする。
- AJ. Observation cache、spatial index、parallelization等の有無で決定論的結果が変化しない。
- AK. 日次CSVは欠落なく継続し、全履歴diagnosticsは設定間隔と世界完了時だけ再集計しても値とSimulation Event列を変えない。

### v0.2.4 unresolved-system closure

SettlementPressure:

1. `UsableInfluenceCells`からMap外、Landmark、侵入不能Cellを除外する。
2. Empty、NPC occupied、Center、通常移動可能Cellを`UsableInfluenceCells`へ含める。
3. `NominalResidentialCapacity = max(1, floor(UsableInfluenceCells * 0.70))`となる。
4. `ResidentLoad`の30日平均人口へ、現在位置を問わずAliveかつActive Affiliationの全所属NPCを含める。
5. `MovementCongestion`へ所属者占有・行先枯渇・Rest Collision・friendly suppressionを数え、Map境界とLandmarkだけのblockを除外する。
6. `ReturnFailure`へCore距離非減少・占有・行先枯渇を数え、Flee / Advance / Defenseを除外する。
7. `0.45 / 0.35 / 0.20`のPressureを30日rollingで日末更新し、翌Tickからだけ有効にする。

Invasion trigger:

8. GenerationではPressureにかかわらずInvasionを開始しない。
9. Order、Active、Support 35以上をすべて要求する。
10. Active Invasion参加中のSettlementから新規Invasionを開始しない。
11. `CrowdingInvasionArmed = true`を要求する。
12. 攻撃可能な別Active Settlementとeligible participant 3名以上を要求する。
13. Pressure `>= 0.65`だけHighPressureDaysを進め、下回ればresetし、既存30日条件を維持する。ただしv0.2.5ではFissionPressureDays 90日とhotspotなしを満たすまで開始しない。
14. 開始時にarmedをfalse、High / Low counterを0とし、新Eventを翌Tickから有効にする。
15. Event終了後かつActive InvasionなしでPressure `<= 0.45`が30日連続するとre-armし、上回ればLow counterをresetし、中間帯では両counterを進めない。
16. targetがHostility、Hostile内Friction、全体Friction、距離、named seed tieの順に従う。

Friction:

17. Frictionは対称、Hostilityは方向性、CurrentFrictionは0～100となる。
18. 平時異Settlement CollisionをPairの日次raw countへ加える。
19. root Explicit Threat Incidentだけを数え、Counterattack等Reactionを二重計上しない。
20. Active Invasion中の両陣営CombatをFrictionへ加えない。
21. `max(10, sqrt(LivingPopulationA * LivingPopulationB))`を日末人口から算出する。
22. Collision weight 1、Threat weight 4、`10 * weighted / scale`、daily cap 5を適用する。
23. 毎日`Current * exp(-ln(2)/180)`でdecayしてからimpulseを加える。
24. Invasion宣言時にFrictionを25%残し、75%消費を記録し、Hostilityを変えない。
25. Pair列挙順、root Event列挙順、thread順を変えても日末Frictionが同じになる。

Mobilization:

26. `Clamp(0.20 + 0.30 * SettlementPressure, 0.20, 0.50)`を使う。
27. Target Forceを`floor(Population * rate + 0.5)`で決定論的に丸める。
28. Alive、Active Affiliation、非Rest、他Invasion非参加だけをeligibleにする。
29. Actual Forceが3未満なら開始しない。
30. `CoreTarget = ceil(ActualForceSize / 2)`となる。
31. Core cohortをAffinity降順、同値named seed tieで選ぶ。
32. Frontier cohortを現在Core外からseed付きrandomで選ぶ。
33. 片側不足時は他側で補充し、50/50を厳密制約にしない。
34. Combat / Action値とcollection順がparticipant選択へ影響しない。

Rest and Center:

35. 本項の旧「Restで永久離脱」はv0.2.5でsupersedeされた。非重傷RestはFieldRest、重傷Rest / FleeはRetreatingとなる。
36. Retreatingだけを同じInvasion Eventへ再参加させない。
37. 非重傷FleeはParticipantを維持し、終了後は別Invasionへ参加できる。
38. Center到達、一時占有、複数日保持のいずれでもAttack Victoryにならない。
39. Usable Core Occupation 50%以上を3日連続でAttack Victoryとし、Center統計の有無が結果を変えない。

## v0.2.5 Rest

1. `RestPressure <= 0`ではRest Candidateを生成しない。
2. `RestNeed <= 2`ではRest Candidateを生成しない。
3. `RestNeed > 2`では既存Utility式で候補評価する。
4. Invasion中も同じ候補生成条件を使う。

## v0.2.5 PersonBelief

1. 1 Subjectにつき1 PersonBeliefとなる。
2. Unknown fieldを0やfalseと区別する。
3. StableCommunication 5でcapacity 150となる。
4. AuraがPersonMemoryCapacityを変更しない。
5. 永久交流Markがcapacityへ反映される。
6. 直接Observationが伝聞より優先される。
7. 365日認識なしで人物recordが削除される。
8. Communicationでの再認識がTTLを更新する。
9. 採用された死亡認知で人物recordが削除される。
10. 低優先の死亡伝聞が有効な直接Alive観測を上書きしない。
11. 死亡認知後の再遭遇で新recordを作る。
12. capacity超過時にHearsayOnlyを優先削除する。
13. Active Threat、同Settlement、DirectObservedを優先保護する。
14. 新人物自身が最下位なら保存されない場合がある。

## v0.2.5 Knowledge categories

1. EventBeliefはMemorable Eventだけから作る。
2. SettlementBeliefは1 Settlementにつき1 recordとする。
3. PersonBelief / EventBelief / SettlementBeliefを別管理する。
4. Event / SettlementはNPC死亡までTTL削除しない。
5. Person死亡EventがPersonBelief削除後もEventBeliefへ残る。
6. Settlement消滅認知でSettlementBeliefを削除せずActiveStatusを更新する。

## v0.2.5 Communication

1. category優先順位をEvent > Settlement > Personとする。
2. Event候補がある間はPerson fieldを先に送らない。
3. Event候補を使い切った後、残り枠をSettlementへ使える。
4. Settlement候補後にPersonへ進める。
5. Person field単位でUnknownを埋める。
6. 送信者が知らないfieldを生成しない。
7. 直接Observation優先規則を受信後も維持する。
8. 既存Distortionを数値fieldへ適用する。
9. SubjectSwapは既知人物だけを対象にする。

## v0.2.5 Incremental Statistics and Event

1. StatisticsをEvent発生時に増分更新する。
2. UI更新時に全Raw Event履歴を再scanしない。
3. Friendly Collision suppression等を日次集計できる。
4. Historical PinがRecent Buffer削除後も残る。
5. Raw Archive無効でもSimulation結果が変わらない。
6. Raw Archive有効でもSimulation結果が変わらない。
7. Recent Buffer容量変更でSimulation結果が変わらない。

## v0.2.5 SettlementSupport

1. SupportPotentialが既存`50P + 30R + 20S`式に従う。
2. Settlement成立時のSupportが50となる。
3. DailySupportDeltaが規定式に従う。
4. SettlementSupportが0～100へClampされる。
5. Support < 25でLowSupportDaysが増える。
6. Support 25～35でLowSupportDaysがfreezeする。
7. Support >= 35でLowSupportDaysが0へ戻る。
8. Support=100かつPotential>=80でSaturatedDaysが増える。
9. 条件を外れるとSaturatedDaysが0へ戻る。
10. 365日継続でSupport 50へRenewalする。
11. Renewal時LowSupportDaysが0となる。
12. RenewalをSettlement消滅として扱わない。

## v0.2.5 Fission

1. Pressure >= 0.40が90日継続するまでFissionしない。
2. 条件中断でFissionPressureDaysが0へ戻る。
3. Order以外ではFissionしない。
4. Support < 35ではFissionしない。
5. Active Invasion中はFissionしない。
6. 5×5 Hotspot条件を正しく判定する。
7. Resident-Days 90未満ではcandidateにしない。
8. 現在Unaffiliated 3人未満ではcandidateにしない。
9. 距離8～24以外をcandidateにしない。
10. 非親SettlementとのInfluence重複を拒否する。
11. 親SettlementとのInfluence overlap例外が動作する。
12. Candidate選択はResident-Daysを優先する。
13. migrantを生存所属者の40%から一様抽選する。
14. 最低4人未満ではFissionしない。
15. FissionFounderを記録する。
16. child SettlementSupportが50となる。
17. migrantへAffinity 10を付与する。
18. child Core内UnaffiliatedへAffinity 7を付与する。
19. Migration Biasがchild Influence到達で解除される。
20. Fission成功時HighPressureDaysが0となる。
21. Fission成立tickに同じ親からInvasionを始めない。

## v0.2.5 ParentChildNonAggression

1. 親子Settlementを相互Invasion targetへ選ばない。
2. 親子所属差だけではExplicit Attack Candidateを作らない。
3. 平時親子CollisionをCombatへ変換しない。
4. 実際にAttackされた場合のCounterattackは有効である。
5. 兄弟Settlementへ不可侵を自動継承しない。
6. 祖父母・孫へ自動継承しない。
7. Settlement消滅・統合で関係を無効化する。

## v0.2.5 Fission / Invasion priority

1. FissionPressureDays 90未満ではPressure由来Invasionを始めない。
2. 有効hotspotがあればFissionを優先する。
3. Fission成立時は同日Invasionを始めない。
4. hotspotがない場合だけInvasion条件を評価する。
5. ParentChildNonAggression対象をInvasion targetから除外する。

## v0.2.5 Invasion state

1. RestPressure <= 0の参加者がRestを選ばない。
2. 非重傷参加者がRestするとFieldRestになる。
3. FieldRestは1日後に元の役割へ復帰する。
4. HP比 <= 0.20でRestするとRetreatingになる。
5. HP比 <= 0.20でFleeするとRetreatingになる。
6. HP比 > 0.20でFleeしても参加状態を維持する。
7. Retreatingは同じInvasionへ再参加しない。
8. FieldRest中もAlive Non-Retreatingへ数える。
9. Invasion終了時に全参加状態を解除する。

## v0.2.5 Invasion combat and victory

1. 敵Participant同士をActive Threatとして扱う。
2. 攻撃側が敵usable Coreへ進む。
3. 防衛側が侵入者へ展開する。
4. Invasion中はHome / Foreign BiasをParticipantへ適用しない。
5. 敵Participant CollisionをCombatへ変換する。
6. Core占領50%未満ではAttackOccupationDaysを0にする。
7. Core占領50%以上が3日連続でAttack Victoryとなる。
8. 1～2日だけ50%以上でも勝利しない。
9. Center占拠だけでは勝利しない。
10. 攻撃軍30%以下が3日連続でDefense Victoryとなる。
11. 防衛Influence内の攻撃Participant 0人が7日連続でDefense Victoryとなる。
12. 90日膠着でDefense Victoryとなる。
13. Damage式がv0.2.4から変化していない。

## v0.2.5 closure: Memorable Event

1. Mandatory Memorable EventはImportance 60未満でもEventBelief候補になる。
2. Mandatory外EventはPin Importance 60以上で候補になる。
3. Importance 59以下では候補にならない。
4. Memorable Eventでも未認識NPCへ自動保存されない。
5. 通常高頻度EventをEventBeliefへ自動保存しない。
6. 既存Importance値がないEventを新規に推測採点しない。

## v0.2.5 closure: SettlementBelief

1. NPCは自SettlementのId / Active / Centerを正確に知る。
2. 自Settlement人口を自動的には知らない。
3. Centerを距離3以内で直接ObservationするとCenterがKnownになる。
4. 所属NPCの直接ObservationでSettlementIdだけの部分recordを作れる。
5. 所属NPC ObservationだけでCenterや人口を自動取得しない。
6. Fission participantが親・child Settlementを認識する。
7. Invasion participantが関連Settlementを認識する。
8. 直接ObservationしたCenterを低優先伝聞で上書きしない。
9. Settlement Dissolution認知でActiveStatusを更新できる。
10. PopulationEstimateをCenter Observationだけから生成しない。

## v0.2.5 closure: Person eviction

1. Unknown fieldをAggregatePersonConfidence上0として扱う。
2. 全tracked field KnownかつConfidence 1ならAggregate 1となる。
3. 半数Unknownならcoverageを反映してAggregateが低下する。
4. AggregatePersonConfidenceをAction Decisionへ直接使用しない。
5. Position Unknownはeviction距離でPositiveInfinityとなる。
6. 他条件が同じならPosition Unknown人物をKnown人物より先に削除する。
7. Active Threat保護を距離順位より先に適用する。
8. 同Settlement保護を距離順位より先に適用する。
9. LastKnownPositionとの距離にChebyshev距離を使う。

## v0.2.5 closure: Fission Center

1. Landmark CellをCenterへ選ばない。
2. Map外・侵入不能Cellを選ばない。
3. 現在NPCがいるCellもDesignation Center候補にできる。
4. Cell Resident-Days最大Cellを優先する。
5. 同値なら現在Unaffiliated NPC在住Cellを優先する。
6. 同値なら5×5幾何中心へ近いCellを優先する。
7. 最終同値をnamed seedで決定論的に解決する。
8. Map scan順で結果が変化しない。
9. Valid Centerなしならhotspot candidateが不成立になる。
10. 次の有効hotspotを同じMaintenanceで評価できる。

## v0.2.5 closure: Migration

1. child Influence内へ入ればMigration完了となる。
2. Core到達を要求しない。
3. Fission成立時にすでにInfluence内なら即時完了する。
4. 2マスMoveでは最終Position後に判定する。
5. Move後完了でMigrationBiasを解除する。
6. Flee後でもInfluence内なら完了できる。
7. Tick末fallbackで取り残された完了状態を解消する。
8. 完了Eventを二重発行しない。
9. 完了時にAffiliation変更を再発生させない。
10. child Settlement消滅でMigrationを中断する。
11. Invasion中はInvasion Biasを優先する。
12. Invasion終了後、未完了Migrationを再開する。

具体的なtest framework、fixture形式、統計的試験のsample数は実装時に決める。
