# ADR-0023: stabilize invasion episodes before redesigning warfare

- **Status:** Superseded by ADR-0024
- **Date:** 2026-08-18

## Context

同一Crowding状態からInvasionが連続し、Centerへ一人到達するだけで征服され、Dead NPCの所属Historyまで書き換わり、Frictionが実質永久値へ増大した。

## Decision

- Invasion開始時にCrowdingInvasionArmedをfalseとし、Pressure 0.70未満が30日連続した後だけ再armする。
- Center単独占拠の即勝利を無効化し、Usable Core 50%以上の占拠だけを暫定Attack Victoryとする。
- 征服所属変更はAlive NPCだけに行い、Dead historyとEventを変更しない。
- Frictionを0～100へClampする。
- RestによるInvasion離脱はRest v2の効果を観測するため維持する。

## Consequences at acceptance

当時は明白な異常だけを抑え、Invasion Trigger、SettlementPressure、Crowding、Friction新モデル、Mobilization、Rest再参加、Center保持Victoryを後続ADRへ保留した。これらのうち現行採用分はADR-0024が置換する。

## Supersession

ADR-0024がPressure trigger `0.65`、re-arm `0.45`、正規化Friction、Mobilization、Rest同一Event再参加禁止、Center非勝利を確定した。Alive-only conquestとUsable Core 50%勝利はADR-0024でも維持する。旧Pressure `0.70`未満30日のre-armと、FrictionをClampするだけの暫定状態は現行仕様ではない。
