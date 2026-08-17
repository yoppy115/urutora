# ADR-0016: settlements emerge before demographic order

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15の長期安定は、秩序のない個体群が大量出生、空間競合、Combat Deathを高回転させることで成立した。これを失敗として消すだけでは、繁殖と滞在から社会が生まれる因果を失う。Order開始後に過去Hotspotを遡及してSettlement化すると、成立時には住民が去っている不自然さも生じる。

## Decision

世界開始時をGeneration（世界生成期 / 萌芽）とし、Generation中から直近のReproduction Success HotspotにSettlementを自然発生させる。Founder、Affinity、所属はGeneration中から存在できるが、Rest、Vitality、Aging、Reproduction、治安等の社会秩序RuleはOrderまで有効化しない。

GenerationからOrderへの移行は絶対人口や固定日数でなく、rolling PopulationCVとBirth / DeathのDemographicImbalanceがConfig条件を連続して満たすことで判定する。

Settlement構造変更は固定順のTick末Maintenanceでcommitし、新規Settlement、WorldPhase、Invasion開始は原則翌Tickから有効にする。同日Hotspot Candidateは一つのimmutable snapshotから生成し、Reproduction Success数を優先、同数だけnamed seedでtie-breakする。Center選択後に既存Settlement排他へ違反したCandidateは別Centerへrerollせず不成立とする。

v0.2.2では、所属者を少なくとも一方に含む繁殖が受胎時にその同一Active Settlement Core内で成立した場合、子を同Core内へ出生させ、当該Settlement所属として開始する。これは遺伝ではなく「繁殖→滞在→集落→所属」の因果を出生世代へ連続させる場所帰属であり、Core外や曖昧な所属候補へは適用しない。

## Reasons

- 繁殖→滞在→集落形成→所属という因果を保つ。
- Settlement形成と社会制度の成熟を別の出来事として扱う。
- 個体生態系の安定をWorld Lifecycle上の秩序成立へ接続する。

## Consequences

- Settlement形成、Affinity、World Phase判定を独立した責務として実装する必要がある。
- Order前後で同じSettlementのRule setが変化する。
- PhaseとSettlement統計をUI / headless diagnosticsへ公開する。
- 日中Eventと日末構造commitを分離し、scan / collection / thread順へ依存しないarbitrationと翌Tick反映を実装する必要がある。
- 条件付きSettlement出生はBirthRequestへ受胎時の一意なSettlement IDを保存し、出生時にもActiveである場合だけ適用する必要がある。
