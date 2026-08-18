# v0.2.6 Fission / Invasion Throughput Update

- **Status:** Baseline / v0.2.6 configurable defaults
- **Decision:** [`ADR-0030`](../decisions/ADR-0030-v026-fission-and-invasion-throughput.md)

## Scope

v0.2.6は、v0.2.5のKnowledge、Support、Fission、Migration、Invasion前線を維持し、Fission hotspotの母集団とInvasionの動員・防衛終了・再発間隔だけを変更する。本書に明記しないゲーム規則はv0.2.5以前の現行正史を維持する。

## Fission hotspot population

Fission用5×5 hotspotの直近30日Resident-Daysと現在人口には、所属を問わず全Alive NPCを数える。

- `Alive NPC 1人 × 1 Cell × 1日 = 1 Resident-Day`。
- Affiliated / Unaffiliated、親Settlement所属、他Settlement所属を集計時点では区別しない。
- 90 Resident-Days、現在3人、30日window、5×5、親Center距離8～24、Core / Influence非重複等の既存条件は維持する。
- hotspotへ数えられたNPCがmigrantになるとは限らない。migrant候補は従来どおり親SettlementのAlive affiliated memberだけである。

Center選択の第一順位もCell別の全Alive NPC Resident-Daysとし、同値なら現在Alive NPCの存在、幾何学中心距離、named seed tieの順を使う。

## Invasion mobilization

既存のPressure由来rateを2倍してTarget Forceを決める。

```text
BaseRate = Clamp(0.20 + 0.30 * SettlementPressure, 0.20, 0.50)
TargetForce = min(
    LivingAffiliatedPopulation,
    DeterministicRound(LivingAffiliatedPopulation * BaseRate * 2.0))
```

丸めは0.5を大きい側へ送る。Alive、Active Affiliation、非Rest、他Invasion非参加、最低3名、Core / Frontier cohort、能力値非選抜は維持する。eligible不足時のActual ForceはTarget Force未満になり得る。

## Distance-scaled defender clear period

Invasion開始時の攻撃・防衛Settlement Center間Chebyshev距離を固定し、Defender Influence内にAlive Non-Retreating attackerがいない状態に必要な連続日数を次で決める。

```text
InfluenceClearRequiredDays = 7 + Ceil(CenterDistance * 1.0)
```

距離と必要日数はEvent作成時に保存し、後のCenter変更や所属統合で遡及変更しない。攻撃戦力比30%以下3日、Core 50%以上3日、90日膠着の条件は維持する。

## Invasion cooldown

`CrowdingInvasionArmed`、低Pressure連続日数、再武装条件を廃止する。攻撃SettlementごとにInvasion開始tickを記録し、次の開始まで60日を要求する。

```text
WorldTick - LastInvasionStartedTick >= 60
```

これは開始から次の開始までの間隔であり、前Event終了から60日ではない。Active Invasion中の新規開始禁止、Order / Support / Pressure / Fission先行 / target / participant条件はすべて維持する。防衛側として参加しただけでは、そのSettlement自身の攻撃cooldownを開始しない。

## Explicit non-changes

- FissionのPressure 0.40 / 90日、40% migrant、最低4名、Migration完了条件を変えない。
- 親子Settlement非侵略、Invasion target順位、Friction消費を変えない。
- Participant state、Rest / Flee、Damage、Hit、Combat、Conquestを変えない。
- Struggle、軍事占領、補給、兵科、能力値による動員優先を追加しない。
