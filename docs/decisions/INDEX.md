# Architecture Decision Records

ADRは、重要で後戻りしづらい判断について「なぜそうしたか」を残す短い記録です。現在の仕様そのものは `docs/design/` と `docs/architecture/` に反映します。

## Records

| ID | Status | Decision |
| --- | --- | --- |
| [`ADR-0001`](ADR-0001-utility-ai.md) | Accepted | Utility上位2〜3候補から、Utility由来の重みで確率選択する |
| [`ADR-0002`](ADR-0002-subjective-decision-boundary.md) | Accepted | 主観的意思決定とRealityによる権威的解決を分離する |
| [`ADR-0003`](ADR-0003-causal-world-lifecycle.md) | Accepted | 困難・適応・副作用の因果連鎖で世界を進行させる |
| [`ADR-0004`](ADR-0004-dual-psalm-inheritance.md) | Accepted | 上位存在本人と詩篇を別系統で継承する |
| [`ADR-0005`](ADR-0005-heritable-genotype-scope.md) | Accepted | 遺伝を基礎能力と正式採用済みUtility評価係数に限定する |
| [`ADR-0006`](ADR-0006-llm-outside-simulation-core.md) | Accepted | LLMを権威的Simulation Coreの外側に置く |

## Future decisions

次は未決のため、まだAccepted ADRを作らない。

- tick内snapshot、競合解決、atomic commit。
- Utilityから非負の選択重みへの変換。
- PRNGと名前付き乱数ストリーム。
- 永続化形式とschema migration。

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

