# Reproduction, Birth, and Genetics

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Purpose and candidate boundary

繁殖は人口増加装置ではなく、子孫を残した性質が次世代へ伝わる淘汰圧である。v0は性別を持たず、距離1以内で次をすべて満たす相手だけを候補にする。

- 両者Aliveかつ成熟済み。
- 両者CurrentHPが各自のEffectiveMaxHPの50%以上。
- 両者ReproductionCooldownが0。
- 行動側ReproductionNeedが4以上。

## Acceptance reaction

行動側がReproduceを選ぶと、相手はAccept / Rejectをsoftmaxで評価する。

```text
U_accept = Need_reproduction
         - 0.40 * Need_survival
         - 0.20 * Need_rest
U_reject = 0
```

成功時は両者のReproduction Needを6減らし、両者にCooldownを開始し、即時生成せずBirthQueueへ追加する。Cooldownのv0 defaultは730日。数値はConfig化する。

行動側の `U_reproduce = P + 0.50*A - 0.40*S - 0.20*R` は [`UTILITY_AI.md`](UTILITY_AI.md) に定義する。複数の有効対象がある場合はseed付きランダムで選び、相手が受諾しそうかをRealityから先読みしない。

Reproduction Need -6とCooldown開始はSuccess時だけ。Reject時にはReproduction NeedとCooldownを変えない。ただしAttemptは通常能動Actionなので、Rejectを含む失敗でも行動側にActivity -2、Rest +0.5を適用する。Accept / Reject Reactionには適用しない。

## Birth queue

Success時にBirthRequestへParentAId、ParentBId、両者のPositionAtConception、GeneticData / seed informationを保存する。後の同日移動で位置基準を変えず、両親がBirth解決前に死亡しても成立済みRequestをキャンセルしない。

Tick末に全Requestをまとめて解決する。各Requestは受胎時の両親隣接Cell和集合にある空きCellからseed付きで希望Cellを選ぶ。同一Cell競合はseed付き決定論的tie-breakで1件だけ勝者とし、敗者は残る候補から再抽選する。全候補が尽きた場合だけ出生失敗となる。queue順、Entity生成順、collection列挙順に依存させない。

Tick中に死亡して空いたCellは利用できるが、LandmarkとAlive NPC占有Cellは利用できない。出生失敗でもSuccess時に発生済みのNeed減少とCooldownは戻さない。子は次Tickから行動する。

ParentAId / ParentBIdを系譜解析用に保存してよいが、親子の特別認識、保護、好意、専用Utility、子育てはv0に含めない。NPC本人が系譜を知る必要もない。

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

## Tests

非遺伝情報が子へ移らないこと、各項目の独立blend、Mutation再現性とClamp、MarkがBase値を変更しないこと、Reject時のNeed/Cooldown不変、Birth位置競合のqueue順非依存と失敗時コスト維持をheadless testで検証する。

採用理由は [`ADR-0005`](../decisions/ADR-0005-heritable-genotype-scope.md) を参照する。
