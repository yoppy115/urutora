# ADR-0013: v0.15 resolves targeted actions before movement

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

初回RunではAttackの約97.45%、Communicationの約73.31%がTargetAbsentとなった。日初Observation後に対象が移動しても古いPositionを使い続け、成立しないActionを同日中に反復していた。

## Decision

Attack、Communication、ReproductionをTargeted Action PhaseとしてMove、Flee、Restより先にResolutionする。Targeted Action内部の固定順序は未決であり、このADRでは決めない。

Attackを受けたNPCは未実行Intentを破棄し、最新の自己State / Perceptionから同一Action枠を最大1回だけ再評価する。Reproduction Rejectは相手Intentを維持し、Acceptだけが同様に最大1回置換する。どちらも追加Actionではない。

対象が既知Position / 距離に存在しなければTargetAbsent Outcomeを返し、行動者の対象PositionをUnknownまたは無効Confidenceへする。死亡や現在位置は自動開示しない。

## Consequences

- TargetAbsent率の低下と、Attack後の状況変化への反応を期待する。
- Interrupt回数、Action枠、TargetAbsent invalidationを明示的に追跡する。
- Targeted Action内部順序が必要な実装は、別途決定するまで保留する。

## Rejected alternatives

### Keep resolving movement before target-dependent actions

観測直後から大量のTargetAbsentを構造的に生む。

### Cancel intent on every Reproduction request

RejectされたAttemptが無料の行動妨害として機能する。

