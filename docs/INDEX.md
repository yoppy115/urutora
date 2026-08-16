# Documentation Index

このページは、現在の正史へ入るための目次です。

## Status labels

- **Baseline**: 元の会話に明記され、初期の作業前提として採用した内容。
- **Draft**: 方向性はあるが、実装に必要な判断が残っている内容。
- **Proposed**: リポジトリ運用を具体化するために追加した提案。合意前のゲーム仕様ではない。
- **Placeholder**: ファイルの役割だけ決まり、仕様本文がまだない内容。
- **Accepted**: ADRとして採用済みの判断。

`Draft` や `Placeholder` の空白は自由な創作を許可するものではありません。実装前に人間と合意して更新します。

## Design

| Document | Status | Purpose |
| --- | --- | --- |
| [`VISION.md`](design/VISION.md) | Baseline | 中核体験と迷ったときに戻る原則 |
| [`WORLD_CYCLE.md`](design/WORLD_CYCLE.md) | Draft | 世界時間とシミュレーション処理順 |
| [`CONCEPTS.md`](design/CONCEPTS.md) | Draft | 概念をコード外データとして扱う方針 |
| [`UTILITY_AI.md`](design/UTILITY_AI.md) | Baseline | NPCの行動候補評価と選択 |
| [`PERCEPTION.md`](design/PERCEPTION.md) | Baseline | RealityとNPCの主観認識の分離 |
| [`REPRODUCTION.md`](design/REPRODUCTION.md) | Draft | 繁殖・継承・突然変異の設計 |

## Architecture

- [`ARCHITECTURE.md`](architecture/ARCHITECTURE.md)（Draft）: レイヤー、依存方向、再現性の境界。
- [`MODULES.md`](architecture/MODULES.md): 想定モジュールと責務。実装技術決定後に具体化する。

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
  -> Codexが実装とテスト
  -> 差分レビュー
  -> mainへ統合
```

設計文書は「今どう動くべきか」、ADRは「なぜその判断をしたか」を記録します。
