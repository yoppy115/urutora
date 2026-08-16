# Modules

**Status:** Baseline responsibilities / Draft APIs

実装技術を選ぶ前の責務一覧。名称や粒度は確定APIではない。

| Module | Responsibility | Must not |
| --- | --- | --- |
| Simulation Runner | 日次phaseとMicro Roundを調整する | ドメイン規則や全状態を抱える |
| Reality Store | 客観状態を構成されたstate sliceとして保持する | NPC向け判断APIを直接提供する |
| Observation | RealityとActionOutcomeから観測事実を生成する | Utilityを評価する |
| Perception Store | NPCごとの主観、記憶、噂、関係を保持・更新する | 未観測Realityを透過参照する |
| Needs | 生存、休息、活動、交流、繁殖の状態を評価する | 行動を直接解決する |
| Utility Decision | 主観情報から候補を評価しActionIntentを作る | RealityやUIへ依存する |
| Action Resolution | IntentをRealityで検証しOutcomeを返す | NPCの知識を意思決定前に補完する |
| Spatial Resolution | Grid占有、Move競合、Collision Attack変換を扱う | UtilityへReality占有状態を漏らす |
| Combat Resolution | Attack、Damage、Counterattack、Pursuit Attackを扱う | Reactionを無限再帰させる |
| Communication | Held Information交換と受信時変形を扱う | 未知Reality情報を生成する |
| Lifecycle / Aging | 年齢、老化、自然死を扱う | 繁殖やUtility式を所有する |
| Reproduction | 繁殖、遺伝、交叉、突然変異、系譜を扱う | 老化方式や乱数源を内部生成する |
| Concept / Difficulty | 概念・困難データと…12676 tokens truncated…同じPerceptionとseedの再現、候補0/1/2件、同点、temperature境界、Top 3外非選択をheadless testで検証する。

採用理由は [`ADR-0001`](../decisions/ADR-0001-utility-ai.md) と [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。

