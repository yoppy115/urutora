# ADR-0009: v0 uses a continuous vitality curve

- **Status:** Superseded by ADR-0014
- **Date:** 2026-08-16

## Context

繁殖と世代交代には自然死可能性が必要だが、回復と老化を無関係な処理にすると不連続または永久生存が起きやすい。

## Decision

v0はAgingStartまで線形に0へ減る自然回復と、その後線形に絶対値が増えるHP減少を一つのDailyVitalChangeとして扱う。RestはHPを直接回復しない。AgingStartAge、HealAtBirth、AgingSlope等の曲線と数値はv0 configurableで、寿命と自然死可能性だけがBaselineである。

## Consequences

- 30歳境界で正、0、負へ連続する。
- ConceptMarkはEffectiveMaxHPを変えて寿命へ間接作用できるが、AgingSlopeやBaseMaxHPを書き換えない。
- 曲線、HP cap、最終的な自然死可能性をheadless testする。

## Supersession

初回Run後、約50年scaleが進化観測に遅すぎると判断した。v0.15では約3年scaleのdata-driven cubic curveへ置き換える。履歴として本ADRを残し、現行仕様には [`ADR-0014`](ADR-0014-short-life-vitality-and-combat-scale.md) を適用する。
