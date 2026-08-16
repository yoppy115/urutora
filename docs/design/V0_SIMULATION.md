# Minimum Simulation v0

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Validation purpose

v0は完成版ではない。単純な主観、欲求、遺伝、空間競合を持つNPCを観測したとき、プレイヤーが「こいつ次どうなる？」と思える創発的逸脱が発生するかを検証する。

対象は、誕生、成長、移動、休息、交流、情報変形、戦闘、逃走、繁殖、遺伝、老化、死亡、Concept Landmarkによる淘汰圧の歪みである。国家、宗教、経済、世界再編、詩篇等は将来正史として維持するが、v0では実装しない。

## Implementation foundation

- 言語とruntimeはC# / .NET。
- `Simulation.Core` はGUIなしで実行・自動テストでき、Realityの唯一の権威となる。
- `Simulation.App` はCoreを操作・観測するDesktop Applicationで、Simulation規則を所有しない。
- `Simulation.Core.Tests` はGUIなしで決定性とドメイン不変条件を検証する。
- UI frameworkと具体デザインは実装時に選択してよい。

## Base stats

| Stat | v0 scale | Meaning |
| --- | --- | --- |
| MaxHP | 100前後 | 耐久と生存余力 |
| Action | 0〜10 | 追加行動、競合優先、移動、追撃へ影響する介入速度 |
| Combat | 0〜10 | 命中、防御、Damage等の戦闘有効性 |
| Communication | 0〜10 | 情報伝達量と情報伝送品質。社交性そのものではない |

Base値とConceptMark適用後のEffective値を分離する。

## Initial world defaults

- 64×64 square grid、8方向、Chebyshev距離。
- 1 Cellは `Empty`、`NPC`、`Landmark` のいずれかで、最大1占有物。
- InitialPopulationは200。
- 3 Landmarkと重複せず、全域と各Landmark周辺が概ね均等になるseed付きstratified random placementを使う。
- 闘争 `(16,16)`、生存 `(48,16)`、交流 `(32,48)` を目安にほぼ対称配置する。座標originに応じた微調整は許す。

初期個体の実験用defaultは中央寄りの分布とする。

| Value | Distribution |
| --- | --- |
| BaseMaxHP | Normal(mean=100, sd=10)、合理的範囲へClamp |
| BaseAction | Normal(mean=5, sd=1.5)、Clamp(0,10) |
| BaseCombat | Normal(mean=5, sd=1.5)、Clamp(0,10) |
| BaseCommunication | Normal(mean=5, sd=1.5)、Clamp(0,10) |
| RiskPreference | Normal(mean=0.5, sd=0.2)、Clamp(0,1) |
| Age | 12〜29歳へ概ね均等なseed付き分布 |

初期CurrentHPはEffectiveMaxHP。Survival Needはそこから導出し概ね0となる。他のNeedは同期を避けるため0〜5程度のseed付きConfig分布を許す。これらは普遍則ではない。

## v0 scope

Grid World、Time、NPC、Needs、Perception、Held Information、Utility AI、Move、Collision Attack、Threat Memory、Rest、Communicationと変形、Attack、Counterattack、Flee、Pursuit、Reproduction、Birth、Genetics、Mutation、Vitality、Aging、Death、Concept Landmarks、Exposure、ConceptMark、Structured Events、Deterministic RNG、Headless Tests、Desktop Observation Appを実装対象とする。

Desktop Observation Appの観測実験用機能として、番号付きWorld run生成、世界別生ログ、数値IDによるNPC選択・詳細表示、人口・平均年齢・選択Action集計を含む。これは世界再編、次世界生成、国家・文化等の新しいSimulation mechanicsを追加するものではない。

## Outside v0

国家、明示的Group/Faction、宗教、経済、本格戦争、疾病、暴戻、不和、高度Culture、親子社会関係、上位存在の継承・簒奪・授与、世界フェーズ・再編、詩篇、啓示、LLM生成、動的新概念生成、本格的美術、3Dは実装しない。

これは上位正史の否定ではない。v0は、観測→予測→期待→逸脱→結果→解釈、Reality→Perception→History→Psalm、世界ライフサイクル、三層進化等を将来成立させるための最小実験である。
