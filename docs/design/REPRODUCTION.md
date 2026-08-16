# Reproduction, Birth, and Genetics

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Purpose and candidate boundary

繁殖は人口増加装置ではなく、子孫を残した性質が次世代へ伝わる淘汰圧である。v0は性別を持たず、距離1以内で次をすべて満たす相手だけを候補にする。

- 両者Aliveかつ成熟済み。
- 両者CurrentHPがEffectiveMaxHPの50%以上。
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

## Birth queue

Tick末に、ParentAとParentBの隣接Cellの和集合から空きCellをseed付きで選ぶ。空きがなければ出生失敗だが、Need減少とCooldownは戻さない。子は次Tickから行動する。出生位置競合はAction競合と同様、列挙順に依存しない決定規則を持つ。

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

非遺伝情報が子へ移らないこと、各項目の独立blend、Mutation再現性とClamp、MarkがBase値を変更しないこと、Birth位置競合と失敗時コスト維持をheadless testで検証する。

採用理由は [`ADR-0005`](../decisions/ADR-0005-heritable-genotype-scope.md) を参照する。

