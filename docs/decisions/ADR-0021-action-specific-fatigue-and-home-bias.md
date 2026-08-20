# ADR-0021: use action-specific fatigue and settlement home bias

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

一律`Rest +0.5`と線形Rest UtilityによりRestが強く、所属者もSettlement周辺へ定住せず、Invasion参加者が短期間で離脱した。

## Decision

v0.2.4はAction種類別の身体疲労、閾値付き対数RestPressure、自Settlement内Move疲労軽減、Weak / Strong Home Bias、平時Foreign avoidanceを採用する。Biasは通常Move候補weightであり、Flee、Advance、Defenseを上書きしない。Active Invasion参加者は攻撃・防衛ともHome / Foreign Biasを受けず、攻撃参加者は敵Core Centerへの1 Cell接近`×5`、不変`×1`、離脱`×0.2`のAdvance Biasを受ける。ReactionはAction枠や通常Activity効果を得ないが、身体的なCounterattack / Pursuit疲労は受ける。

Generation Settlementには同所属Collision抑制、正Vitality`×1.25`、通常Affinity gain`×2`を含む限定Proto-Orderを与え、Order専用Benefitと区別する。

## Consequences

Restと帰巣が実際の活動・負傷・社会空間へ接続される。数値はv0.2.4 Configとして再調整できるが、優先例外とProto-Order / Order境界は維持する。
