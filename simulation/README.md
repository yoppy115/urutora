# Simulation Data

シミュレーションの調整値とゲームデータを、実装コードから分離して管理します。

## Directories

- `configs/`: v0 default。人口、Need、Utility、行動、戦闘、繁殖、突然変異、老化、Concept等。
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

## v0 adopted defaults

次は最初のSimulation用に採用したConfig値であり、不変のゲーム思想ではない。実装時にschema化し、実験結果に応じて調整できるようにする。

| Area | Defaults |
| --- | --- |
| World | 64×64、InitialPopulation 200、1 tick/day、365 days/year |
| Action | max 5/day、repeat `Action/(Action+5)`、second step `0.02*Action` |
| Utility | Top 3、softmax temperature configurable |
| Threat | memory 365 days |
| Reproduction | MatureAge 12 years、Cooldown 730 days、Need threshold 4、HP ratio 0.50 |
| Mutation | chance 0.10、stddev 0.25 |
| Vitality | AgingStart 30 years、HealAtBirth 0.10 HP/day、AgingSlope about 3.75e-6 HP/day^2 |
| Concept | exposure 1.0/0.5/0.25、threshold 100、effective multiplier 1.2 |

Need増減、Communication変形、Combat、Pursuit、初期分布等の全defaultは各設計文書を正本とする。まだコード実装を行わないため、この変更では `default.json` を作らない。

## Run metadata

将来、再現用metadataを保存する場合は最低限次を扱えるようにする。ただしファイル出力自体はv0初期必須要件ではない。

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
