# ADR-0014: v0.15 uses a short-life cubic vitality curve and half HP scale

- **Status:** Accepted
- **Date:** 2026-08-17
- **Supersedes:** ADR-0009

## Context

旧約50年の寿命では世代交代と遺伝分布変化の観測が遅すぎた。一方、BaseMaxHPだけを半減すると、既に強かったCombat死亡圧がさらに増える。

## Decision

自然寿命targetを約3年へ短縮し、旧v0の線形回復・線形老化を廃止する。複数Age Control Point間を滑らかなCubic interpolationで結ぶdata-driven DailyVitalChange curveを使い、確定済みのLife Phase形状へ従う。

BaseMaxHP中心scaleを約100から約50へ、Damageを次へ同時変更する。

```text
Damage = max(
  1,
  4 + 0.9 * EffectiveCombat_attacker
    - 0.4 * EffectiveCombat_defender)
  * Random(0.9, 1.1)
```

Hit Rate、Counterattack構造、ConceptMark倍率は変更しない。MatureAge 180日、Cooldown 90日、ReproductionNeedGain +0.04/day、InitialAge 180〜700日をv0.15 configurable defaultとする。

## Configurable defaults

各Vitality Control Pointの具体的DailyVitalChange値はv0.15 Configとし、Codexが保守的な初期値を設定してよい。curveの連続性、確定Phaseの符号・強弱、BaseMaxHP約50、若年の軽傷回復、1.5歳以降回復なし、3歳前後から自然死急増、不連続大量死なしを満たす必要がある。

## Consequences

- 約1095 tickのscaleで世代交代を観測する。
- HPとDamageを同時scaleし、おおよそのTime-To-Killを維持する。
- Control Point値はSimulation Run後に再調整でき、ゲーム思想上の固定値にしない。
