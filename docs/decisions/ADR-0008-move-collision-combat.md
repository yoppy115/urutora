# ADR-0008: moving into an occupied NPC cell becomes combat

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

占有CellをMove候補から除外すると、関係や敵対が存在しない初期世界で暴力が自然発生しにくい。

## Decision

NPC占有CellをMove候補から除外せず、Resolution時にMove IntentをCollision Attackへ変換して通常Attack Resolutionを使う。移動はキャンセルし、対象が死亡しても同じAction内では進入しない。被攻撃者は攻撃者をPerceivedThreatとして記憶し、後の明示的AttackやFleeへ接続する。

通常MoveのUtilityと方向は、Perception上で占有NPCを知っていてもv0では変更しない。Collision Attackは通常Attack同様にCounterattackを発生させ得る。攻撃側はOutcomeから相手EntityId、Position、戦闘結果を直接知る。

同じ空きCellのMove競合敗者はUtilityへ戻らず移動先だけ再抽選し、占有Cellなら同じ変換を行う。

## Consequences

空間競合→最初の暴力→Threat Memory→継続的敵対という説明可能な因果が生まれる。MoveとCombatは直接結合せず、Spatial Resolutionがtyped intentをCombat Resolutionへ渡す。

v0.2のGeneration中は本判断を維持する。Orderでは同Settlement、Influence内Unaffiliated、異Settlement平時Collisionを社会秩序Ruleが抑制またはFrictionへ変換し、Invasion中の敵同士はCombatへ戻る。この後続条件は [`ADR-0017`](ADR-0017-settlement-conflict-and-invasion.md) に記録する。
