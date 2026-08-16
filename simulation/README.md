# Simulation Data

シミュレーションの調整値とゲームデータを、実装コードから分離して管理します。

## Directories

- `configs/`: 基本設定。人口、意思決定間隔、突然変異率、老化等。
- `presets/`: 初期状態や実験条件の名前付き組み合わせ。
- `concepts/`: 概念と困難のデータ定義。

## Configuration policy

次は安全に実験を重ねるためのBaseline constraintsであり、具体schemaは実装技術決定時に確定する。

- 設定にはschema versionを持たせる。
- 実行開始時に検証し、未知のキーや不正値を黙って無視しない。
- 実行中に使った完全な設定を実験結果へコピーする。
- default値が変わっても、過去の実験を元の値で再実行できるようにする。
- 単位をキー名またはschemaで明示する。
- mutation rate、Utility選択、老化等の調整値をコードへ埋め込まない。

## Values mentioned in source conversations

次のJSONは過去の会話に登場した**例**であり、採用済みのバランス値ではない。

```json
{
  "population": 150,
  "mutationRate": 0.03,
  "decisionInterval": 2.0,
  "topUtilityCandidates": 3,
  "agingHpDecay": 0.002
}
```

寿命が必要であることはBaselineだが、`agingHpDecay` を含む具体的な老化方式と値はDraftである。そのため現時点では `default.json` を作らない。

## Run metadata

将来の各実行では、最低限次を保存する。

```json
{
  "schemaVersion": 1,
  "seed": 8147291,
  "config": "configs/default.json",
  "preset": "presets/example.json",
  "ticks": 0,
  "commit": "git-commit-hash"
}
```

完全なconfig、初期状態、外部入力列を参照または同梱できるようにする。保存形式はDraftである。

