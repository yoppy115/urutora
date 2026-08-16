# ADR-0008: moving into an occupied NPC cell becomes combat

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

占有CellをMove候補から除外すると、関係や敵対が存在しない初期世界で暴力が自然発生しにくい。

## Decision

NPC占有CellをMove候補から除外せず、Resolution時にMove IntentをCollision Attackへ変換して通常Attack Resolutionを使う。移動はキャンセルし、対象が死亡しても同じAction内では進入しない。被攻撃者は攻撃者をPerceivedThreatとして記憶し、後の明示的AttackやFleeへ接続する。

同じ空きCellのMove競合敗者はUtilityへ戻らず移動先だけ再抽選し、占有Cellなら同じ変換を行う。

## Consequences

空間競合→最初の暴力→Threat Memory→継続的敵対という説明可能な因果が生まれる。MoveとCombatは直接結合せず、Spatial Resolutionがtyped intentをCombat Resolutionへ渡す。

