# Modules

**Status:** Draft

実装技術を選ぶ前の責務一覧。名称や粒度は確定APIではない。

| Module | Responsibility | Must not |
| --- | --- | --- |
| Simulation Runner | tick、処理順、world lifecycleを調整する | ドメイン規則や全状態を抱える |
| Reality Store | 客観状態を構成されたstate sliceとして保持する | NPC向け判断APIを直接提供する |
| Observation | RealityとActionOutcomeから観測事実を生成する | Utilityを評価する |
| Perception Store | NPCごとの主観、記憶、噂、関係を保持・更新する | 未観測Realityを透過参照する |
| Needs | 生存、休息、活動、交流、繁殖の状態を評価する | 行動を直接解決する |
| Utility Decision | 主観情報から候補を評価しActionIntentを作る | RealityやUIへ依存する |
| Action Resolution | IntentをRealityで検証しOutcomeを返す | NPCの知識を意思決定前に補完する |
| Lifecycle / Aging | 年齢、老化、自然死を扱う | 繁殖やUtility式を所有する |
| Reproduction | 繁殖、遺伝、交叉、突然変異、系譜を扱う | 老化方式や乱数源を内部生成する |
| Concept / Difficulty | 概念・困難データと世界進化を扱う | 表示名を安定IDとして使う |
| World Lifecycle | 萌芽から次世界までの状態と遷移を扱う | 単純な全能崩壊ゲージだけで遷移させる |
| Higher Entity | 上位存在、影響圏、加護を扱う | 通常NPCとして世界を直接支配する |
| Revelation Intake | プレイヤーの啓示を世界の認識入力へ変換する | NPCへ直接命令する |
| Pin / History | 重要イベントの索引と住人による歴史を構築する | Realityの完全因果を捏造する |
| Inheritance | 簒奪・授与と次世界への実体継承を扱う | 詩篇と本人を同一データに潰す |
| Psalm Generator | Historyとピンから詩篇入力を構成する | Simulation Coreの結果を変更する |
| Narrative Adapter | LLMまたはfallbackで人間可読文を生成する | 世界状態の権威を持つ |
| Configuration | schema検証済み設定とゲームデータを供給する | 実行中に暗黙のglobal状態になる |
| Event Log | 機械可読な世界内イベントを記録する | ドメインの結果を変える |
| Research Exporter | 注目実験の再現情報と要約を保存する | 巨大な生ログを正史へ混ぜる |
| Player Observation | 現在中心のviewと文章表現を作る | Simulation Coreから参照される |
| Presentation Adapter | ゲームエンジン・UIへ接続する | Simulation Coreから参照される |

## Cross-module contracts

- queryは読み取り専用、commandは意図、eventは起きた事実として区別する。
- ActionIntentとActionOutcomeを別の型にする。
- IDは保存、ログ、replay、次世界継承をまたいで安定させる。
- interfaceへ渡すデータは必要最小限にし、内部状態オブジェクトを丸ごと共有しない。
- 時刻と乱数は外部依存として注入し、テストで差し替えられるようにする。
- エラー時には再現に必要なseed、config識別子、tick、entity IDを報告する。
- Narrative Adapterは読み取り専用の入力を受け、ドメインへcommandを返さない。

## Open architecture work

- Realityのstate sliceとmutation ownership。
- tick内で各moduleが読めるsnapshotと書き込み権限。
- domain eventの配送順と失敗処理。
- LLMを使わない場合の文章生成fallback。
- module間の許可依存表とarchitecture test。

