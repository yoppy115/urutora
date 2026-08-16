# Lifecycle, Vitality, and Aging

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Baseline

繁殖と世代交代のため寿命と自然死可能性を必須とし、Lifecycle / Agingが年齢、自然回復、老化、死亡を所有する。RestはHP回復を所有しない。

v0では自然回復と老化を、AgingStartで0を通る連続的な `DailyVitalChange` として扱う。この具体曲線と数値はv0 defaultであり、上位の不変思想ではない。

## Before AgingStart

```text
Age < AgingStartAge:
DailyVitalChange = HealAtBirth * (1 - Age / AgingStartAge)
```

v0 defaultはAgingStartAge 30歳、HealAtBirth +0.10 HP/day。出生時に最大回復し、年齢とともに線形低下して30歳で0となる。CurrentHPはEffectiveMaxHPを超えない。

## At and after AgingStart

```text
Age >= AgingStartAge:
DailyVitalChange = -AgingSlope * (AgeDays - AgingStartDays)
```

老化後は通常自然回復を加算せず、日次減少量が年齢とともに線形増加する。v0参考値は `AgingSlope ~= 3.75e-6 HP/day^2`。BaseMaxHP約100の無傷個体が、老化だけでも概ね50歳前後で自然死し得る程度を目標とする。

## Lifecycle defaults

- MatureAge: 12歳。
- AgingStartAge: 30歳。
- 想定自然寿命: 概ね50歳前後。
- ReproductionCooldown: 730日。

固定50歳死亡ではない。BaseMaxHP、ConceptMark、Combat Damage、自然回復、老化によって死亡年齢は変化する。生存MarkのEffectiveMaxHP ×1.2はAgingSlope自体を変えないため、結果として長寿になり得る。

HealAtBirth、AgingSlope、AgingStartAge、MatureAge、CooldownはConfig化する。

## Tick boundary and tests

Vitality / AgingはConcept Exposure後、Birth Queue前に更新する。CurrentHPが0以下になった時点で即座にDeadとなり、Cell占有と同Tickの行動資格を失う。Tick末Death phaseはDeath Eventとcollection cleanupを確定する。

30歳境界で符号が正→0→負へ連続的に変化すること、EffectiveMaxHP cap、老化だけでも最終的に死亡可能なこと、Deadが後続Micro Roundへ参加しないこと、同seed・同Configの再現をheadless testで検証する。

採用理由は [`ADR-0009`](../decisions/ADR-0009-continuous-vitality-curve.md) を参照する。
