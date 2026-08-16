# Architecture Decision Records

ADRは、重要で後戻りしづらい判断について「なぜそうしたか」を残す短い記録です。現在の仕様そのものは `docs/design/` と `docs/architecture/` に反映します。

## Records

| ID | Status | Decision |
| --- | --- | --- |
| [`ADR-0001`](ADR-0001-utility-ai.md) | Accepted | Utility上位2〜3候補から重み付き確率選択する |

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

