# Modules

**Status:** Draft

実装技術を選ぶ前の責務一覧。名称や粒度は確定APIではない。

| Module | Responsibility | Must not |
| --- | --- | --- |
| Simulation Runner | lifecycleとフェーズ順を調整する | ドメイン規則や全機能を抱える |
| Reality Store | 客観的な世界状態を保持する | NPC向け判断APIを直接提供する |
| Observation | Realityから観測結果を生成する | Utilityを評価する |
| Perception Store | NPCごとの主観状態を保持・更新する | 未観測Realityを透過参照する |
| Utility Decision | 主観情報から行動候補を評価・選択する | RealityやUIへ依存する |
| Action Resolution | 選択された行動を検証しRealityへ作用させる | NPCの知識を後付けで改変する |
| Reproduction | 繁殖・継承・突然変異を扱う | 乱数源や調整値を内部生成する |
| Configuration | schema検証済み設定とゲームデータを供給する | 実行中に暗黙のglobal状態になる |
| Event Log | 世界内イベントを記録する | ドメインの結果を変える |
| Research Exporter | 注目実験の再現情報と要約を保存する | 巨大な生ログを正史へ混ぜる |
| Presentation Adapter | プレイヤー向けviewへ変換する | Simulation coreから参照される |

## Cross-module contracts

- queryは読み取り専用、commandは意図、eventは起きた事実として区別する。
- IDは保存・ログ・replayをまたいで安定させる。
- interfaceへ渡すデータは必要最小限にし、内部状態オブジェクトを丸ごと共有しない。
- 時刻と乱数は外部依存として注入し、テストで差し替えられるようにする。
- エラー時には再現に必要なseedとconfig識別子を報告する。

