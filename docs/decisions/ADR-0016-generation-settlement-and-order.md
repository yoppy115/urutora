# ADR-0016: settlements emerge before demographic order

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15の長期安定は、秩序のない個体群が大量出生、空間競合、Combat Deathを高回転させることで成立した。これを失敗として消すだけでは、繁殖と滞在から社会が生まれる因果を失う。Order開始後に過去Hotspotを遡及してSettlement化すると、成立時には住民が去っている不自然さも生じる。

## Decision

世界開始時をGeneration（世界生成期 / 萌芽）とし、Generation中から直近のReproduction Success HotspotにSettlementを自然発生させる。Founder、Affinity、所属はGeneration中から存在できるが、Rest、Vitality、Aging、Reproduction、治安等の社会秩序RuleはOrderまで有効化しない。

GenerationからOrderへの移行は絶対人口や固定日数でなく、rolling PopulationCVとBirth / DeathのDemographicImbalanceがConfig条件を連続して満たすことで判定する。

## Reasons

- 繁殖→滞在→集落形成→所属という因果を保つ。
- Settlement形成と社会制度の成熟を別の出来事として扱う。
- 個体生態系の安定をWorld Lifecycle上の秩序成立へ接続する。

## Consequences

- Settlement形成、Affinity、World Phase判定を独立した責務として実装する必要がある。
- Order前後で同じSettlementのRule setが変化する。
- PhaseとSettlement統計をUI / headless diagnosticsへ公開する。
- Candidate arbitrationとtick内commit順は別途確定が必要である。
