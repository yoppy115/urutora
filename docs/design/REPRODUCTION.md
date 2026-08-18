# Reproduction, Birth, and Genetics

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Purpose and subjective candidate boundary

繁殖は人口増加装置ではなく、子孫を残した性質が次世代へ伝わる淘汰圧である。v0.15も性別を持たない。

Decision / Candidate生成は対象についてPerception上の情報だけを使う。次を満たすと思っている相手は候補になり得る。

- Perception上Alive。
- Perception上距離1。
- Perception上Mature。

相手のReality上のHP、Cooldown、実距離を候補生成で参照しない。

行動者自身については正確な自己Stateを使い、既存のAlive、Mature、HP 50%以上、Cooldown 0、ReproductionNeed 4以上をCandidate前提にできる。主観境界の変更対象は相手側情報である。

Reality Resolutionは次を権威的に検証する。

- 両者Aliveかつ成熟済み。
- 現在距離1。
- 両者CurrentHPが各自のEffectiveMaxHPの50%以上。
- 両者ReproductionCooldownが0。
- 行動側ReproductionNeedが4以上。

Reality条件を満たさない場合、行動者は「現在はReproductionが成立しなかった」というOutcomeを得る。Cooldown残日数、対象の正確なHP、死亡、現在位置等、直接知り得ない内部理由を自動開示しない。位置不成立にはTargetAbsent規則を適用する。

Reproduction PhaseはAttack Phase後、Communication Phase前に処理する。全Reproduction IntentをAttack後の最新Realityで再Validationし、AttackによるTarget死亡、HP条件不成立、距離等のprecondition failureをFailure Outcomeにする。

## Perceived life stage

直接Observation等から対象を最低限Child / Matureとして主観認識できる。Candidate生成に正確なAgeDaysは不要である。Old等の追加LifeStage分類は必要になるまでDraftとする。

## Acceptance reaction

行動側がReproduceを選ぶと、相手はAccept / Rejectをsoftmaxで評価する。

```text
U_accept = Need_reproduction
         - 0.40 * Need_survival
         - 0.20 * Need_rest
U_reject = 0
```

Order中は、参加者2名が同一Active Settlement Core内にいる場合だけPenaltyなしとする。両者とも外、片方だけCore内、異なるCore、Core境界をまたぐ場合は、v0.2 defaultとして行動側 `U_reproduce -= 2.0`、受諾側 `U_accept -= 2.0` を適用する。Generation中はPenaltyなし。MembershipではなくActionの社会空間を基準とし、Reality成功率へ別のrandom penaltyを追加しない。Candidateの主観境界とReality Validation条件は変更しない。

成功時は両者のReproduction Needを6減らし、両者にCooldownを開始し、即時生成せずBirthQueueへ追加する。Cooldownのv0.15 defaultは90日。数値はConfig化する。

行動側の `U_reproduce = P + 0.50*A - 0.40*S - 0.20*R` は [`UTILITY_AI.md`](UTILITY_AI.md) に定義する。複数の有効対象がある場合はseed付きランダムで選び、相手が受諾しそうかをRealityから先読みしない。

Reproduction Need -6とCooldown開始はSuccess時だけ。Reject時にはReproduction NeedとCooldownを変えない。Attemptは通常能動Actionなので、Rejectを含む失敗でも行動側にActivity -2を適用する。Rest疲労はv0.2.4でAction別となり、Reproduction Attemptは成功・失敗を問わず`Rest +0.35`。Accept / Reject Reactionには通常Action用Activity / Rest変化を適用しない。

Rejectでは対象の既存未実行Intentを維持する。Acceptでは対象のIntentを破棄し、最新の自己State / PerceptionでUtility AIを同一Micro Round最大1回再評価して同じAction枠を置き換える。追加Actionは得ない。

再抽選Intentは終了済みphaseへ巻き戻さない。Accept後の新Intentが現在Micro Roundの未処理phaseで実行可能な場合だけ実行し、該当Actionのphaseが終了済みならそのMicro Roundでは失効する。

## Birth queue

Success時にBirthRequestへParentAId、ParentBId、両者のPositionAtConception、GeneticData / seed informationを保存する。後の同日移動で位置基準を変えず、両親がBirth解決前に死亡しても成立済みRequestをキャンセルしない。

Tick末に全Requestをまとめて解決する。各Requestは受胎時の両親隣接Cell和集合にある空きCellからseed付きで希望Cellを選ぶ。同一Cell競合はseed付き決定論的tie-breakで1件だけ勝者とし、敗者は残る候補から再抽選する。全候補が尽きた場合だけ出生失敗となる。queue順、Entity生成順、collection列挙順に依存させない。

Tick中に死亡して空いたCellは利用できるが、LandmarkとAlive NPC占有Cellは利用できない。出生失敗でもSuccess時に発生済みのNeed減少とCooldownは戻さない。子は次Tickから行動する。

ParentAId / ParentBIdを系譜解析用に保存してよいが、親子の特別認識、保護、好意、専用Utility、子育てはv0に含めない。NPC本人が系譜を知る必要もない。

### v0.2.3 Settlement birth affiliation

出生所属は受胎時に判定し、BirthRequestへ対象Settlementと配置範囲を固定する。

- 両親が同じActive Settlement所属なら、現在位置に関係なく通常の親近傍へ出生し、そのSettlementのMembershipThreshold相当AffinityとActive Affiliationを持って開始する。
- 片親だけが所属する場合、受胎時に両親ともそのActive SettlementのInfluence内にいる場合だけ、受胎時親近傍かつInfluence内のCellへ出生し同所属で開始する。
- 両親の所属が異なる場合、両者の所属IDのうち、受胎時の両親がともにCore内にいるActive Settlementが一意に1件ある場合だけ、そのCore内へ出生し同所属で開始する。

Birth解決時に対象Settlementが非Activeなら通常の無所属・親近傍出生へfallbackする。親のAffinity値そのものは複製しない。位置候補が尽きた場合は通常のBirth Failureであり、境界外へfallbackして所属出生を成立させない。

## Heritable allowlist

v0で遺伝するのは次だけである。

- BaseMaxHP
- BaseAction
- BaseCombat
- BaseCommunication
- RiskPreference

危険選好以外のUtility評価係数は、将来正式採用された場合だけallowlistへ追加できる。

非遺伝はCurrentHP、Age、現在Needs、Memory、Perception、Held Information、Threat Memory、Culture、Learned Results、Relations、Concept Exposure、ConceptMarkである。

## Child values and mutation

各遺伝項目を独立に次で求める。

```text
ChildValue = Lerp(ParentAValue, ParentBValue, RandomBlend) + Mutation
RandomBlend in [0, 1]
```

v0 defaultは `mutationChance = 0.10`、`mutationStdDev = 0.25`。0〜10能力はClamp(0,10)、RiskPreferenceはClamp(0,1)。MaxHPは別scaleのため能力と同程度の相対的変異になるよう扱う。すべてseedで再現可能にする。

ConceptMarkによるEffective補正はBase遺伝値を書き換えず、子へ継承しない。

Settlement Affinity、Active Affiliation、Founder状態、Invasion参加、Auraも遺伝対象ではない。Settlement Core内のReproduction Successは、当事者へv0.2 ConfigのSettlement Affinityを加え、Hotspot生成判定の機械可読Eventとなる。

v0.2.5のFissionでは移住者を親Settlement所属の生存個体から選び、child AffiliationとAffinityを設定するが、遺伝・親子NPC関係・出生規則を変更しない。`FissionFounder`、Migration Bias、親子Settlement関係も非遺伝である。

## Tests

非遺伝情報が子へ移らないこと、各項目の独立blend、Mutation再現性とClamp、MarkがBase値を変更しないこと、Candidateが対象RealityのHP/Cooldownを読まないこと、Reality precondition検証、Reject時のIntentとNeed/Cooldown不変、Accept時最大1回のIntent置換、Birth位置競合のqueue順非依存と失敗時コスト維持をheadless testで検証する。

採用理由は [`ADR-0005`](../decisions/ADR-0005-heritable-genotype-scope.md) を参照する。
