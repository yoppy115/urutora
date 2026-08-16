# ADR-0013: v0.15 resolves targeted actions before movement

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

初回RunではAttackの約97.45%、Communicationの約73.31%がTargetAbsentとなった。日初Observation後に対象が移動しても古いPositionを使い続け、成立しないActionを同日中に反復していた。

## Decision

Attack、Reproduction、CommunicationをTargeted Action Phaseとして、この固定順でMove、Flee、Restより先にResolutionする。Attackは不可逆なHP / Alive / Intent変更、ReproductionはAccept時のIntent置換、Communicationは主に情報変更を行うため、この順序とする。

Attackを受けたNPCは未実行Intentを破棄し、最新の自己State / Perceptionから同一Action枠を最大1回だけ再評価する。Reproduction Rejectは相手Intentを維持し、Acceptだけが同様に最大1回置換する。どちらも追加Actionではない。

対象が既知Position / 距離に存在しなければTargetAbsent Outcomeを返し、行動者の対象PositionをUnknownまたは無効Confidenceへする。死亡や現在位置は自動開示しない。

各後続phaseは先行phase後のRealityを再Validationする。Interruptで再抽選されたIntentは、現在Micro Roundでまだ未処理の適切なphaseだけで実行できる。終了済みphaseへ巻き戻さず、実行機会がなければ失効する。

## Consequences

- TargetAbsent率の低下と、Attack後の状況変化への反応を期待する。
- Interrupt回数、Action枠、TargetAbsent invalidationを明示的に追跡する。
- Attack後に死亡・HP条件不成立となった対象へのReproduction / Communicationを成立させない。
- 再抽選Attackを同じAttack Phaseへ戻さず、再帰連鎖を防ぐ。

## Rejected alternatives

### Keep resolving movement before target-dependent actions

観測直後から大量のTargetAbsentを構造的に生む。

### Cancel intent on every Reproduction request

RejectされたAttemptが無料の行動妨害として機能する。
