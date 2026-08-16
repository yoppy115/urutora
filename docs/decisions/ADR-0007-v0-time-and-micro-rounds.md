# ADR-0007: v0 uses daily ticks with bounded Micro Rounds

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

Actionを介入速度として機能させつつ、日次の観測と老化を分かりやすく保ち、Entity列挙順による隠れた優先度を避ける必要がある。

## Decision

1 tickを1日、365 tickを1年とする。日初のSnapshotとObservation後に、Needs、候補、Utility、Intent、競合、Reality反映を処理する。Actionに基づく追加行動成功者だけが、候補生成からReality反映までをMicro Roundとして反復する。日初以外は完全再観測せず、直接経験したOutcomeだけを即時反映する。v0は1 NPC 1日最大5 Actionとするが、この上限と確率式はConfigである。

競合はAction優先、完全同値は用途別seed付き乱数で解決し、コレクション順に依存しない。

Action関連の確率と競合優先にはEffectiveActionを使う。通常能動Actionは成否に関係なくMicro RoundとNeed costを消費し、Reactionは消費しない。

HP 0以下はResolution時点で即時Deadとなり、後続行動資格とCell占有を失う。Tick末Death phaseはEventとcollection cleanupを担う。BirthRequestは受胎時状態を固定し、Tick末に全Requestを順序非依存でbatch解決する。

## Consequences

- 高Action個体は古い情報のまま複数行動でき、それを逸脱要因として許容する。
- 日次phaseごとのread/write setとReaction深さを実装で明示する。
- 競合の順序非依存と最大Action数をheadless testする。
- 即時Death eligibility、死亡Cell再利用、Birth queue順非依存をheadless testする。
