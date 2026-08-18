# Architecture Decision Records

ADRは、重要で後戻りしづらい判断について「なぜそうしたか」を残す短い記録です。現在の仕様そのものは `docs/design/` と `docs/architecture/` に反映します。

## Records

| ID | Status | Decision |
| --- | --- | --- |
| [`ADR-0001`](ADR-0001-utility-ai.md) | Accepted | 候補数に応じて最大Top 3をUtility由来の重みで確率選択する |
| [`ADR-0002`](ADR-0002-subjective-decision-boundary.md) | Accepted | 主観的意思決定とRealityによる権威的解決を分離する |
| [`ADR-0003`](ADR-0003-causal-world-lifecycle.md) | Accepted | 困難・適応・副作用の因果連鎖で世界を進行させる |
| [`ADR-0004`](ADR-0004-dual-psalm-inheritance.md) | Accepted | 上位存在本人と詩篇を別系統で継承する |
| [`ADR-0005`](ADR-0005-heritable-genotype-scope.md) | Accepted | 遺伝を基礎能力と正式採用済みUtility評価係数に限定する |
| [`ADR-0006`](ADR-0006-llm-outside-simulation-core.md) | Accepted | LLMを権威的Simulation Coreの外側に置く |
| [`ADR-0007`](ADR-0007-v0-time-and-micro-rounds.md) | Accepted | 日次tickと上限付きMicro Roundを採用する |
| [`ADR-0008`](ADR-0008-move-collision-combat.md) | Accepted | NPC占有CellへのMoveをCollision Attackへ変換する |
| [`ADR-0009`](ADR-0009-continuous-vitality-curve.md) | Superseded | 旧v0の線形回復・老化曲線。ADR-0014が置換 |
| [`ADR-0010`](ADR-0010-csharp-core-and-observation-app.md) | Accepted | C# Core、Desktop App、headless testsを分離する |
| [`ADR-0011`](ADR-0011-partitioned-deterministic-rng.md) | Accepted | 乱数streamを決定論的な用途別keyで分割する |
| [`ADR-0012`](ADR-0012-concept-landmarks-and-selection.md) | Accepted | LandmarkがMarkを介して淘汰を間接的に歪める |
| [`ADR-0013`](ADR-0013-targeted-actions-and-interrupts.md) | Accepted | Attack→Reproduction→Communication、上限付きInterrupt、phase非巻戻し |
| [`ADR-0014`](ADR-0014-short-life-vitality-and-combat-scale.md) | Accepted | 約3年のcubic Vitality、Config初期値制約、HP/Damage同時再scale |
| [`ADR-0015`](ADR-0015-bounded-held-information.md) | Superseded | 旧Subject + Property 3件FIFO。ADR-0025が置換 |
| [`ADR-0016`](ADR-0016-generation-settlement-and-order.md) | Accepted | Generation中形成、日末Maintenance、決定論的Hotspot arbitration、人口安定Order |
| [`ADR-0017`](ADR-0017-settlement-conflict-and-invasion.md) | Accepted | Friction、Unaffiliated保護、同一Core繁殖、Crowding由来Invasionと占領・離脱 |
| [`ADR-0018`](ADR-0018-concept-aura-social-transmission.md) | Accepted | Concept Auraの社会伝播、同種抑制、一時MaxHP normalization |
| [`ADR-0019`](ADR-0019-log-retention-and-build-provenance.md) | Accepted | 完了ログ圧縮とclean Git provenanceをRunへ結び付ける |
| [`ADR-0020`](ADR-0020-deterministic-parallel-read-phases.md) | Accepted | 分離read / planning phaseを決定論的merge付きで並列化する |
| [`ADR-0021`](ADR-0021-action-specific-fatigue-and-home-bias.md) | Accepted | Action別疲労、Home Bias、Generation Proto-Orderを採用する |
| [`ADR-0022`](ADR-0022-local-settlement-support-and-hysteresis.md) | Accepted | 局所SettlementSupportとHysteresisで自然消滅を判定する |
| [`ADR-0023`](ADR-0023-v024-invasion-stabilization-guardrails.md) | Superseded | v0.2.4の暫定Invasion guardrail。ADR-0024が置換 |
| [`ADR-0024`](ADR-0024-settlement-pressure-and-invasion-closure.md) | Accepted | SettlementPressure、Invasion hysteresis、正規化Friction、Mobilization、Center非勝利を確定 |
| [`ADR-0025`](ADR-0025-structured-knowledge-and-person-memory.md) | Accepted | Person / Event / Settlement知識分離、人物capacity / TTL、差分Communication |
| [`ADR-0026`](ADR-0026-event-retention-and-incremental-statistics.md) | Accepted | Recent Event、増分統計、Milestone、optional archiveを分離 |
| [`ADR-0027`](ADR-0027-accumulated-support-renewal-and-fission.md) | Accepted | 累積Support、Renewal、Fission先行、直接親子の平時非侵略 |
| [`ADR-0028`](ADR-0028-invasion-field-rest-and-sustained-victory.md) | Accepted | FieldRest / Retreating、Core前線、継続占領・防衛Victory |

## Future decisions

次は未決のため、まだAccepted ADRを作らない。

- 永続化形式とschema migration。
- Desktop UI framework。
- domain event配送方式とstate sliceの具体API。

## Workflow

1. `TEMPLATE.md` を複製し、次の連番を付ける。
2. 代替案と影響を含めて `Proposed` として議論する。
3. 採用時に `Accepted`、不採用時に `Rejected` とする。
4. 採用した仕様を同じ変更で設計・アーキテクチャ文書へ反映する。
5. 後から判断を変える場合、古いADRを書き換えて歴史を消さず、新しいADRで `Superseded` にする。

## Status values

- `Proposed`
- `Accepted`
- `Rejected`
- `Superseded`
- `Deprecated`
