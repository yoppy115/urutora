# ADR-0023: stabilize invasion episodes before redesigning warfare

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

同一Crowding状態からInvasionが連続し、Centerへ一人到達するだけで征服され、Dead NPCの所属Historyまで書き換わり、Frictionが実質永久値へ増大した。

## Decision

- Invasion開始時にCrowdingInvasionArmedをfalseとし、Pressure 0.70未満が30日連続した後だけ再armする。
- Center単独占拠の即勝利を無効化し、Usable Core 50%以上の占拠だけを暫定Attack Victoryとする。
- 征服所属変更はAlive NPCだけに行い、Dead historyとEventを変更しない。
- Frictionを0～100へClampする。
- RestによるInvasion離脱はRest v2の効果を観測するため維持する。

## Consequences

明白な異常を抑えつつ、Invasion Trigger、SettlementPressure、Crowding、Friction新モデル、Mobilization、Rest再参加、Center保持Victoryは後続ADRまで確定しない。
