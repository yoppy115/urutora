# Documentation Index

このページは、現在の正史へ入るための目次です。

## Status labels

- **Baseline**: 現時点で採用済みの設計原則または仕様。
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
| [`PERCEPTION.md`](design/PERCEPTION.md) | Baseline constraints / Draft mechanics | Reality、主観、意思決定境界 |
| [`PLAYER_OBSERVATION.md`](design/PLAYER_OBSERVATION.md) | Baseline constraints / Draft mechanics | 現在中心UI、文章表現、ピン |
| [`WORLD_LIFECYCLE.md`](design/WORLD_LIFECYCLE.md) | Baseline constraints / Draft mechanics | 世界フェーズ、困難、再編、継承 |
| [`SIMULATION_TICK.md`](design/SIMULATION_TICK.md) | Draft | 1 tickの処理順と競合解決 |
| [`CONCEPTS.md`](design/CONCEPTS.md) | Baseline constraints / Draft mechanics | 概念と困難、初期3概念 |
| [`UTILITY_AI.md`](design/UTILITY_AI.md) | Baseline constraints / Draft mechanics | 欲求、主観評価、確率選択 |
| [`REPRODUCTION.md`](design/REPRODUCTION.md) | Baseline constraints / Draft mechanics | 繁殖、遺伝境界、突然変異 |
| [`LIFECYCLE_AGING.md`](design/LIFECYCLE_AGING.md) | Baseline constraints / Draft mechanics | 寿命、老化、自然死 |
| [`PSALM_AND_INHERITANCE.md`](design/PSALM_AND_INHERITANCE.md) | Baseline constraints / Draft mechanics | 歴史、詩篇、啓示、上位存在、継承 |

## Architecture

- [`ARCHITECTURE.md`](architecture/ARCHITECTURE.md)（Baseline constraints / Draft mechanics）: レイヤー、依存方向、再現性の境界。
- [`MODULES.md`](architecture/MODULES.md)（Draft）: 想定モジュールと責務。
- [`LOGGING.md`](architecture/LOGGING.md)（Baseline constraints / Draft mechanics）: 機械可読ログと研究保存。

## Decisions

- [`decisions/INDEX.md`](decisions/INDEX.md): ADR一覧と運用規則。

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

