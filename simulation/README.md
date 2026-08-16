# Simulation Data

シミュレーションの調整値とゲームデータを、実装コードから分離して管理します。

## Directories

- `configs/`: v0.15 default。人口、Need、Utility、行動、戦闘、繁殖、突然変異、Vitality Curve、Concept等。
- `presets/`: 初期状態や実験条件の名前付き組み合わせ。
- `concepts/`: 概念と困難のデータ定義。

`configs/observation-app.json` はSimulation規則ではなくDesktop Appの観測・保存設定を持つ。schema 3ではdefault seed、Worldごとのseed増分、`logs/` directory、World番号の桁数、Event表示上限、graph保持点数、NPC行動履歴表示上限、現在年齢分布のbin幅、完了World圧縮、旧release log削除を管理する。App release versionはassembly versionから取得し、`logs/vX.Y/` の分離とversion内World番号に使用する。

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

Need増減、Utility Effect、Threat Risk、Observation誤差・Confidence、Communication Confidence、Communication変形、Combat、Pursuit、初期分布等の全defaultは各設計文書を正本とする。これらの数値はv0.15 configurableであり、主観境界、Base/Effective分離、即時Dead、TargetAbsent invalidation、順序非依存競合等のBaselineと混同しない。実装用の完全なschemaと採用値は `configs/v0-default.json` に置き、未知keyや不正値を起動時に拒否する。

Vitality Control Point値は確定Phase形状と連続性等の制約を満たす保守的なv0.15 Config初期値として設定し、Simulation Run後に再調整する。

## Run metadata

Desktop Appは各観測Worldについて `logs/vX.Y/world-NNNN/` に再現用metadataと使用Configを保存する。最低限次の情報を扱う。

```json
{
  "schemaVersion": 4,
  "releaseVersion": "v0.15",
  "seed": 8147291,
  "worldNumber": 1,
  "worldId": "world-0001",
  "configId": "v0.15-default-1",
  "repositoryCommit": "full-40-character-commit",
  "repositoryTreeState": "clean",
  "simulationConfigSha256": "...",
  "observationAppConfigSha256": "..."
}
```

現在は完全なSimulation ConfigとObservation App Configを同梱し、EventをJSONL、日次統計をCSV、診断をJSONLで保存する。完了後は`world-NNNN.zip`とSHA-256 sidecarへ置換し、連番はarchiveも含めて継続する。世界snapshot、外部入力列、再開形式はDraftのままである。
