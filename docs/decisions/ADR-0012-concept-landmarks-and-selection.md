# ADR-0012: v0 Concept Landmarks distort natural selection indirectly

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

上位存在が世界を直接操作せず「杭」として環境を歪める上位正史を、最小Simulationで検証可能な形にする必要がある。

## Decision

闘争、生存、交流を固定・進入不可のLandmarkとして置き、距離別Exposureから永久ConceptMarkを与える。MarkはEffective能力だけを補正し、Base遺伝値を変更せず、Mark自体も遺伝しない。

遺伝は常にBase値、Simulation上の実能力はEffective値を使う。EffectiveActionは行動頻度・競合・移動・追撃、EffectiveCombatはReality戦闘、EffectiveCommunicationは送信量とClamp済み品質、EffectiveMaxHPはNeed・HP比・繁殖条件・HP上限へ使う。MaxHP Mark取得時はCurrentHP絶対値を維持し、増加分を即時補充しない。

## Consequences

Mark個体の生存・行動・戦闘・交流・繁殖機会が変わり、その個体のBase形質が間接的に残りやすくなる。効果倍率、Exposure速度、閾値、配置はv0 configurableである。

v0.2はExposure radiusを4へ拡張し、Mark Holderから同一Settlement所属者への非stack一時Auraを追加する。本人Markの1.2倍、Base非変更、非遺伝は維持する。社会伝播判断は [`ADR-0018`](ADR-0018-concept-aura-social-transmission.md) に記録する。
