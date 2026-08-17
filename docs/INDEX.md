# Documentation Index

このページは、現在の正史へ入るための目次です。

## Status labels

- **Baseline**: 現時点で採用済みの設計原則または仕様。
- **v0.x default / configurable**: 各Simulation版の実験を成立させる採用済み初期値。Configで変更でき、普遍的な世界法則ではない。
- **Baseline constraints / Draft mechanics**: 境界と目的は確定しているが、数式、閾値、データ構造、アルゴリズムが未決。
- **Draft**: 実装に必要な判断が残り、現時点では仕様として固定しない内容。
- **Proposed**: 合意前の提案。ADRでは採用候補を示す。
- **Placeholder**: ファイルの役割だけ決まり、仕様本文がまだない内容。
- **Accepted**: ADRとして採用済みの判断。

`Draft` や `Placeholder` の空白は自由な創作を許可するものではありません。BaselineとDraftが同じ文書にある場合は、節ごとの状態を優先します。

## Design

| Document | Status | Purpose |
| --- | --- | --- |
| [`VISION.md`](design/VISION.md) | Baseline | 制作目的、中核体験、逸脱、三層進化 |
| [`PERCEPTION.md`](design/PERCEPTION.md) | Baseline / v0 configurable | Reality境界、観測誤差、Confidence、Held Information |
| [`PLAYER_OBSERVATION.md`](design/PLAYER_OBSERVATION.md) | Baseline constraints / Draft mechanics | 現在中心UI、文章表現、ピン |
| [`WORLD_LIFECYCLE.md`](design/WORLD_LIFECYCLE.md) | Baseline constraints / Draft mechanics | 世界フェーズ、困難、再編、継承 |
| [`V0_SIMULATION.md`](design/V0_SIMULATION.md) | Baseline / v0 configurable | v0の目的、範囲、初期世界、実装基盤 |
| [`V0_15_ECOLOGY.md`](design/V0_15_ECOLOGY.md) | Baseline / v0.15 configurable | 初回Runに基づく生態系変更と解消済み実装規則 |
| [`V0_2_SETTLEMENT_ORDER.md`](design/V0_2_SETTLEMENT_ORDER.md) | Baseline / v0.21 configurable | Settlement自然発生、v0.21 Hotspot補正、Maintenance、Order、Friction、Invasion、Aura |
| [`SIMULATION_TICK.md`](design/SIMULATION_TICK.md) | Baseline / v0 configurable | 日、Micro Round、即時Death、Birth競合、乱数 |
| [`V0_ACTIONS.md`](design/V0_ACTIONS.md) | Baseline / v0 configurable | Action/Reaction、Move、Communication、Combat、Flee、Pursuit |
| [`CONCEPTS.md`](design/CONCEPTS.md) | Baseline constraints / Draft mechanics | 概念と困難、初期3概念 |
| [`UTILITY_AI.md`](design/UTILITY_AI.md) | Baseline / v0 configurable | Action別主観Utility、Threat Risk、上位3候補softmax |
| [`REPRODUCTION.md`](design/REPRODUCTION.md) | Baseline / v0 configurable | 繁殖、出生、遺伝、突然変異 |
| [`LIFECYCLE_AGING.md`](design/LIFECYCLE_AGING.md) | Baseline / v0 configurable | 寿命、連続Vitality曲線、自然死 |
| [`PSALM_AND_INHERITANCE.md`](design/PSALM_AND_INHERITANCE.md) | Baseline constraints / Draft mechanics | 歴史、詩篇、啓示、上位存在、継承 |

## Architecture

- [`ARCHITECTURE.md`](architecture/ARCHITECTURE.md)（Baseline constraints / Draft mechanics）: レイヤー、依存方向、再現性の境界。
- [`MODULES.md`](architecture/MODULES.md)（Draft）: 想定モジュールと責務。
- [`LOGGING.md`](architecture/LOGGING.md)（Baseline constraints / Draft mechanics）: 機械可読ログと研究保存。
- [`TESTING.md`](architecture/TESTING.md)（Baseline test obligations / Draft implementation）: v0のheadless不変条件。
- [`STATISTICS.md`](architecture/STATISTICS.md)（Baseline observation obligations / Draft storage）: v0.2のWorld、Settlement、Invasion、Aura診断。

## Decisions

- [`decisions/INDEX.md`](decisions/INDEX.md): ADR一覧と運用規則。

## Implementation register

- [`IMPLEMENTATION_REGISTER.md`](IMPLEMENTATION_REGISTER.md): 正史外のtooling、workflow、UI・保存形式等の交換可能な実装詳細。

## Ideas

- [`ideas/BACKLOG.md`](ideas/BACKLOG.md): 正史ではない保留案と却下案。

## Update rule

```text
ChatGPTで議論
  -> 人間が採用判断
  -> design文書を更新
  -> 重要ならADRを追加
  -> Codexが変更と検証
  -> 差分レビュー
  -> mainへ統合
```

設計文書は「今どう動くべきか」、ADRは「なぜその判断をしたか」を記録します。
