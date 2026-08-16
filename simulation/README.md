# Simulation Data

シミュレーションの調整値とゲームデータを、実装コードから分離して管理します。

## Directories

- `configs/`: 基本設定。人口、意思決定間隔、突然変異率など。
- `presets/`: 初期状態や実験条件の名前付き組み合わせ。
- `concepts/`: 概念のデータ定義。

## Proposed configuration policy

次は安全に実験を重ねるためのリポジトリ運用案であり、実装時にschemaと一緒に確定する。

- 設定にはschema versionを持たせる。
- 実行開始時に検証し、未知のキーや不正値を黙って無視しない。
- 実行中に使った完全な設定を実験結果へコピーする。
- default値が変わっても、過去の実験を元の値で再実行できるようにする。
- 単位をキー名またはschemaで明示する。

## Values mentioned in the source conversation

元の会話には次のJSONが**例**として登場したが、採用済みのバランス値ではない。

```json
{
  "population": 150,
  "mutationRate": 0.03,
  "decisionInterval": 2.0,
  "topUtilityCandidates": 3,
  "agingHpDecay": 0.002
}
```

そのため、現時点では `default.json` を作らない。各値の意味・単位・有効範囲を決めた後にschemaと一緒に追加する。

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

この形も仮案であり、実装言語と保存形式を決める際に確定する。
