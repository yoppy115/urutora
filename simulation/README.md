# Simulation Data

シミュレーションの調整値とゲームデータを、実装コードから分離して管理します。

## Directories

- `configs/`: v0.2 default。人口、Need、Utility、行動、戦闘、繁殖、Vitality、Settlement、Order、Invasion、Aura等。
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

## v0.15 adopted defaults

次は初回v0 Runを受けて採用したv0.15 Config値であり、不変のゲーム思想ではない。旧v0のThreat 365日、Mature 12年、Cooldown 730日、約50年寿命、BaseMaxHP約100、線形Vitalityはv0.15で置き換える。

| Area | Defaults |
| --- | --- |
| World | 64×64、InitialPopulation 200、1 tick/day、365 days/year |
| Action | max 5/day、repeat `EffectiveAction/(EffectiveAction+5)`、second step `Clamp(0.02*EffectiveAction,0,1)` |
| Utility | Top 3、softmax temperature configurable |
| Utility effects | Move/Rest/Communication/Attack/Flee/ReproductionのNeed係数、Threat Risk係数 |
| Observation | 距離別error 5%/7.5%/10%、Confidence 1.00/0.90/0.80 |
| Communication confidence | factor `0.50 + 0.03 * Clamp(EffectiveCommunication,0,10)` |
| Targeted phase | Attack → Reproduction → CommunicationをMove / Flee / Restより先に解決 |
| Threat | memory 90 days |
| Reproduction | MatureAge 180 days、Cooldown 90 days、Need gain +0.04/day、Need threshold 4、HP ratio 0.50 |
| Mutation | chance 0.10、stddev 0.25 |
| Vitality | 約3年scale、複数Age Control Point間のsmooth cubic curve。具体値は制約付きConfig調整 |
| HP / Damage | BaseMaxHP center about 50、Damage `max(1, 4 + 0.9*A - 0.4*D) * Random(0.9,1.1)` |
| InitialAge | 180–700 days |
| Held Information | max 3 records per Subject + Property、FIFO eviction、直接消滅確認時にSubject purge |
| Concept | exposure 1.0/0.5/0.25、threshold 100、effective multiplier 1.2 |

Need増減、Utility Effect、Threat Risk、Observation誤差・Confidence、Communication Confidence、Communication変形、Combat、Pursuit、初期分布等の全defaultは各設計文書を正本とする。これらの数値はv0.15 configurableであり、主観境界、Base/Effective分離、即時Dead、TargetAbsent invalidation、順序非依存競合等のBaselineと混同しない。まだコード実装を行わないため、この変更では `default.json` を作らない。

Vitality Control Point値は確定Phase形状と連続性等の制約を満たす保守的なv0.15 Config初期値として設定し、Simulation Run後に再調整する。

## v0.2 adopted defaults

v0.15値は明示変更箇所以外を維持し、次をv0.2 Configへ追加する。

| Area | Defaults |
| --- | --- |
| Settlement formation | 90-day Reproduction Success window、4×4、threshold 4、15-day evaluation、Center spacing 7 |
| Region | Core radius 3、Influence radius 7、Rest Collision radius 5 |
| Affinity | Founder +10、initial resident +7、membership 10、switch margin +5、Stay +0.05/day、Rest +1、Communication +0.5、Reproduction Success +2 |
| Generation → Order | 90-day window、PopulationCV 0.10、DemographicImbalance 0.20、30 consecutive days |
| Order benefits | Rest ×1.5、positive Vitality ×2、negative Vitality ×0.5、outside U_reproduce / U_accept -2 |
| Relations / crowding | Initial Hostility 30%、Crowding 0.5 Occupancy + 0.5 BlockedMovement、threshold 0.70 for 30 days |
| Mobilization | `Clamp(0.20 + 0.30 * CrowdingPressure, 0.20, 0.50)` |
| Dissolution | <=10% World Population for 365 consecutive days |
| Concept | Exposure radius 4 with 1/0.5/0.25/0.125、Aura radius 2、Rest -0.10/day、stat ×1.1 |

Advance / Cohesionの具体WeightはConfig / implementation detailだがAdvanceを主とする。Hotspot arbitration、Friction具体値、Aura同種合成等の未決事項をConfig defaultの名目で独自確定しない。

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
