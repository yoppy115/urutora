# Version Lineage

この文書は、実験ログと正史を同じ版名だけで混同しないための版系譜を定める。

## v0.2 series

| Version | Name | Canonical change |
| --- | --- | --- |
| v0.2 | Settlement / Order Update | Generation中のSettlement形成、Order、Friction、Invasion、Aura |
| v0.2.1 | Settlement Formation Minor Update | Hotspotを90日・5×5・成功3件・15日評価へ調整 |
| v0.2.2 | Settlement Observation / Birth Affiliation Update | 出生所属、Mark表示、速度段階、観測・ログ・履歴改善 |
| v0.2.3 | Settlement Stability / Performance Update | Settlement重複防止、観測UI、近傍index、決定論的並列化 |
| v0.2.3 supplement | Birth Affiliation Rule補完 | 同所属、片親所属、異所属の出生所属境界を確定 |
| v0.2.4 | Settlement Stabilization Update | Rest v2、帰巣、Proto-Order、局所Support、Invasion暫定安定化 |
| v0.2.4 closure patch | Unresolved Systems Closure | SettlementPressure、Trigger / re-arm、正規化Friction、Mobilization、Center非勝利を確定 |
| v0.2.5 | Knowledge, Fission & Invasion Update | 三種Knowledge、増分統計、累積Support / Renewal、Fission先行、持続型Invasion |
| v0.2.5 closure patch | Unresolved Contracts Closure | Memorable / Settlement知識、Person eviction、Fission Center、Migration完了、Struggle延期を確定 |

## Run identity

Run比較では少なくとも次を一組として扱う。

- `Version`
- `repositoryCommit`
- `Config`
- `Seed`

同じVersion名でも`repositoryCommit`が異なるログを、同一実装世代の結果として混在させない。
