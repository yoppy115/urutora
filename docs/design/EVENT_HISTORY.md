# Event Retention, Statistics, and History Inputs

- **Status:** Baseline / technical policy
- **Decision:** [`ADR-0026`](../decisions/ADR-0026-event-retention-and-incremental-statistics.md)

## Four layers

Simulation Eventの用途を四層へ分ける。

| Layer | Purpose | Lifetime |
|---|---|---|
| Recent Event Buffer | UI、直近因果、debug | 有限ring / buffer |
| Incremental Statistics | Game内統計、Run比較、終了判定 | Event受領時に更新 |
| Historical Milestones / Pins | History / Psalmの長期入力 | 長期保持 |
| Optional Raw Archive | 外部研究・完全追跡 | Core外へstream可能 |

Recent Buffer容量、archiveのflush・分割・圧縮は、Simulation結果へ影響しない技術裁量である。Raw ArchiveをCoreの必須stateにしない。

## Retention policy

次の高頻度Eventは日次・期間集計を基本とし、個別永続保存を必須にしない。

- 同Settlement Collision suppression、MoveFailed、Unaffiliated protection。
- Home / Foreign / Migration Bias適用。
- 個別Communication field転送、Candidate failure、friendly collision。
- Observation更新、Affinity gain。

次は原則として個別Event / Milestoneを残す。

- Birth、Death、CombatDeath、ConceptMark。
- Settlement Formation、Renewal、Fission、Dissolution、Integration。
- Invasion開始・終了、Conquest、World Phase。
- 重要ActionOutcome、Pin、大規模人口変動。

何をImportant / Pinと判定する具体閾値は未決である。

## Incremental statistics

Statistics projectionはEventや日末snapshot差分を受けて増分更新する。rolling値は日次ring等で保持し、UI、診断、Completion判定のたびに全raw historyを再scanしない。

最低限、次を集計可能にする。

### Knowledge

- category別record数、NPCあたりPersonBelief平均・最大。
- capacity、TTL expiry、eviction、保護対象eviction、新record即時eviction。
- field別Observation / Communication更新、優先度で拒否した更新。
- Unknown比率、Death採用によるPersonBelief削除。
- category別送信数、差分送信数、候補枯渇。

### Settlement support and fission

- `SupportPotential`、`SettlementSupport`、`DailySupportDelta`。
- SaturatedDays、Renewal回数・日、Renewal間隔、Renewal後人口・Pressure。
- LowSupportDays、P / R / S各成分、Supportが100に達した日。
- FissionPressureDays、hotspot候補数、不成立理由。
- 5×5 Resident-Days、現在Unaffiliated数、候補Center。
- migrant予定・到着・死亡・bias解除、親子Settlement、Fission回数。
- Fission成功日、同日Invasion抑止、hotspotなしによるInvasion移行。

### Invasion

- Event別InitialAttackForce、現AliveNonRetreating、Influence内攻撃者数。
- participant state別日数・遷移、FieldRest、Retreating、Death。
- attack / defense front、Core占有率と連続日数。
- Attack Victory、Defense A / B / C、終了理由、継続日数。
- Collision / Explicit / Counter / Pursuit Attack、Damage、CombatDeath。
- Pressure / Fission gate、target、mobilization、Friction retention。

### World observation

- 人口、出生、死亡、Settlement数、形成・更新・分裂・消滅・統合。
- Expansion指標、Pressure分布、Invasion数。これらはStruggle Phase判定そのものではない。

## Non-interference

Recent Buffer容量、UI表示件数、archive有無、圧縮、統計表示頻度を変えても、同じCode / Config / RunSeedのSimulation Event列と最終stateは変化してはならない。
