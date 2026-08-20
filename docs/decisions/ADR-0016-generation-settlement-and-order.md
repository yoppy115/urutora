# ADR-0016: settlements emerge before demographic order

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

v0.15の長期安定は、秩序のない個体群が大量出生、空間競合、Combat Deathを高回転させることで成立した。これを失敗として消すだけでは、繁殖と滞在から社会が生まれる因果を失う。Order開始後に過去Hotspotを遡及してSettlement化すると、成立時には住民が去っている不自然さも生じる。

## Decision

世界開始時をGeneration（世界生成期 / 萌芽）とし、Generation中から直近のReproduction Success HotspotにSettlementを自然発生させる。Founder、Affinity、所属はGeneration中から存在する。後続のADR-0021は、Settlementを根付かせる限定Proto-OrderだけをGenerationから有効化し、Order専用Benefitとの分離を維持する。

GenerationからOrderへの移行は絶対人口や固定日数でなく、rolling PopulationCVとBirth / DeathのDemographicImbalanceがConfig条件を連続して満たすことで判定する。

Settlement構造変更は固定順のTick末Maintenanceでcommitし、新規Settlement、WorldPhase、Invasion開始は原則翌Tickから有効にする。同日Hotspot Candidateは一つのimmutable snapshotから生成し、Reproduction Success数を優先、同数だけnamed seedでtie-breakする。Center選択後に既存Settlement排他へ違反したCandidateは別Centerへrerollせず不成立とする。

Settlement出生所属は、両親が同じActive Settlement所属なら受胎位置を問わず通常の親近傍へ出生し、そのSettlement所属として開始する。片親だけが所属する場合は、受胎時に両者がそのActive Settlement Influence内にいる場合に限り、同Influence内へ出生させ同所属で開始する。両親の所属が異なる場合は、両者が同じ一意なActive Settlement Core内にいる従来条件だけを認め、同Core内へ出生させる。親のAffinity数値は複製せず、曖昧な候補へ所属を付与しない。

v0.2.1の採用defaultは90日・5×5・Success 3・15日評価。v0.2.3は既存Influence内Successを除外し、新Core全Cellを既存Active Influenceと非重複にする。

## Reasons

- 繁殖→滞在→集落形成→所属という因果を保つ。
- Settlement形成と社会制度の成熟を別の出来事として扱う。
- 個体生態系の安定をWorld Lifecycle上の秩序成立へ接続する。

## Consequences

- Settlement形成、Affinity、World Phase判定を独立した責務として実装する必要がある。
- Order前後で同じSettlementのRule setが変化する。
- PhaseとSettlement統計をUI / headless diagnosticsへ公開する。
- 日中Eventと日末構造commitを分離し、scan / collection / thread順へ依存しないarbitrationと翌Tick反映を実装する必要がある。
- 条件付きSettlement出生はBirthRequestへ受胎時の一意なSettlement IDと配置範囲（親近傍 / Core / Influence）を保存し、出生時にもActiveである場合だけ適用する必要がある。
