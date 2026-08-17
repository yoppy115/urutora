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
- Communicationが送信者のHeld Information外から情報を作らない。
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
- Held InformationはSubject + Propertyごとに3件を超えず、4件目で最古をFIFO削除する。
- MatureAge 180日、ReproductionCooldown 90日、ThreatMemoryDuration 90日、ReproductionNeedGain +0.04/dayをConfig defaultとして検証する。
- BaseMaxHP約50 scaleと新Damage係数 `4 + 0.9*AttackCombat - 0.4*DefenseCombat`、Random(0.9,1.1)を検証する。
- Hit Rate、Counterattack構造、Concept Exposure / Mark値がv0.15で変化していないことを回帰検証する。
- InitialAgeが180〜700日のConfig範囲からseed付き生成される。

### v0.15 resolved patch

- 4件目のHeld Information取得で最古記録を削除し、低Confidenceの新情報でもFIFO順を変えない。
- Subject死亡を直接確認すると全Propertyを削除する。
- TargetAbsentではPositionだけを無効化し、Subject全体を削除しない。
- Communicationによる死亡伝聞だけではSubject全体を削除しない。
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
- P. SettlementPressure 0.65以上の30日継続と全前提が成立するとInvasion Eligibleになる。
- Q. Invasion対象がHostility、Friction、Distance、seed tie-breakの優先順位に従う。
- R. MobilizationRateがSettlementPressureに応じ20〜50%で変化する。
- S. Core CohortをCore内のAffinity上位から選び、同値をseed付きで解決する。
- T. Frontier CohortをCore外の所属NPCから選ぶ。
- U. Rest中NPCをInvasion参加候補から除外する。
- V. Advance ParticipantがRestするとBiasを解除しEventから離脱する。
- W. Advance Biasを保持するAlive NPCが0になるとDefense Victoryになる。
- X. Core 50%以上占拠でAttack Victoryになる。Center単独占拠はv0.2.4で勝利条件から除外する。
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
- Patch Q. FleeしてもInvasion ParticipantとAdvance Biasを維持する。
- Patch R. RestでAdvance Bias / Invasion Participantを解除し、同一Eventへ再参加させない。
- Patch S. Death、Event終了、Victory、統合時にParticipant状態を解除する。

Settlement Maintenance順と翌Tick反映、Aura / Core占有計算もcollection order、scan order、thread schedulingに依存しないことを検証する。

### v0.2.1–v0.2.3 adopted minors

- Hotspotは90日、5×5、Success 3、15日評価で、旧4×4 / Success 4より成立可能になる。
- 既存Active Settlement Influence内のSuccessをHotspotから除外する。
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
- P. Generation中でも同Settlement Collision Attackを抑制する。
- Q. Generation Coreの正Vitalityが`×1.25`。
- R. Generationの通常Affinity gainが`×2`。
- S. Founder `+10` / Initial Core `+7`をGeneration multiplierで二重化しない。
- T. Order移行後はGeneration`×1.25`でなくOrder正Vitality`×2`になる。
- U. `SettlementSupport = 50P + 30R + 20S`。
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
- AG. Usable Core 50%占拠でAttack Victoryになる。
- AH. Frictionを0～100へClampする。
- AI. Rest v2導入後もRestによるInvasion離脱Ruleを維持する。
- AJ. Observation cache、spatial index、parallelization等の有無で決定論的結果が変化しない。

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
13. Pressure `>= 0.65`だけHighPressureDaysを進め、下回ればresetし、30日連続を要求する。
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

35. RestでAdvance BiasとParticipant状態を解除する。
36. Rest離脱者を同じInvasion Eventへ再参加させない。
37. Flee / Communication / Reproductionでは離脱せず、終了後の別Invasionには参加できる。
38. Center到達、一時占有、複数日保持のいずれでもAttack Victoryにならない。
39. Usable Core Occupation 50%以上だけでAttack Victoryになり、Center統計の有無が結果を変えない。

具体的なtest framework、fixture形式、統計的試験のsample数は実装時に決める。

