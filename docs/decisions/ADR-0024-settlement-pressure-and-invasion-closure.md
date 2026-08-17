# ADR-0024: close settlement pressure, friction, and invasion rules

- **Status:** Accepted
- **Date:** 2026-08-18
- **Supersedes:** ADR-0017の旧Crowding / raw Friction detail、ADR-0023

## Context

v0.2.4はInvasion連打、Center一人到達、Friction無制限化を暫定guardrailで抑えたが、Trigger、Pressure、Friction更新、Mobilizationが未決だった。Core Occupancy単独では、所属人口全体の負荷、移動詰まり、帰還失敗を表現できず、Settlementの生活圧からInvasionへ至る因果が弱い。

## Decision

- Invasion Triggerの入力を旧CrowdingPressureから、30日ResidentLoad / MovementCongestion / ReturnFailureを統合するSettlementPressureへ置換する。CoreOccupancyは観測と占領診断に残す。
- Order、Active、Support 35以上、armed、Active Invasionなし、targetあり、eligible 3名以上を前提とし、Pressure 0.65以上の30日連続で開始する。開始時にdisarmし、終了後Pressure 0.45以下の30日連続でre-armする。
- Frictionは対称Pair、Hostilityは方向性stateのまま、平時Collisionとroot Explicit Threatを日次人口scaleし、半減期180日で指数decayする。Active Invasion combatは除外し、宣言時にFrictionを25%残す。
- MobilizationはPressureから20～50%を算出し、Alive / Active Affiliation / 非Rest / 他Invasion非参加のeligibleだけから最低3名を選ぶ。Core半数をAffinity優先、Frontierをseed randomとし、不足を相互補充する。Combat / Action値で選抜しない。
- Rest離脱者は同じEventへ再参加しない。Centerは到達、一時占有、保持を含めて勝利条件にせず、Usable Core 50%占拠だけをAttack Victoryとする。
- Concept / AuraとHeld Informationは現行値・境界を意図的に維持する。

## Reasons

- Settlementの生活圧を人口、局所移動、帰還の観測可能な因果へ分解する。
- 生成規模の違うSettlement PairでもFrictionの増減を比較可能にする。
- Invasionを同じ高圧episodeから連打せず、cohort選択を全知能力選抜へしない。
- Centerを象徴的な観測点に留め、勝敗を実際のCore占領へ結び付ける。

## Consequences

- 日末MaintenanceはPressure、Friction、trigger counterを固定順でcommitし、翌Tickから反映する。
- Configは住宅容量比、Pressure weight / threshold、Friction weight / half-life / retention、Mobilization係数を明示する。
- Statistics / Eventは分子・分母、rejection reason、root incident、cohort選択、Center非勝利を追跡する。
- Center保持Victory、軍事占領、同一EventへのRest再参加、能力値による軍事選抜はBacklogへ残す。


