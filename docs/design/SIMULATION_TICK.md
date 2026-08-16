# Simulation Tick and Micro Rounds

**Status:** Baseline boundaries / v0 default and configurable mechanics

## Time baseline

- 1 Simulation Tick = 1日、365 Tick = 1年。
- 内部時刻は整数Tick。Presentationでは `year = floor(tick / 365)`、`day = tick % 365 + 1` とする。
- Tick 0は第0年1日、Tick 365は第1年1日。
- うるう年は扱わない。1 Actionはその日に行った意味のある行動の抽象化である。

## One-day transaction

1. Reality Snapshot
2. Observation
3. Perception更新
4. Needs更新
5. Action Candidate生成
6. Utility評価
7. ActionIntent生成
8. Targeted Action Phase
9. Interrupt / Reaction / 必要な再判断
10. Movement Phase
11. Rest
12. Reality / Event処理
13. Actionによる追加Micro Round判定
14. 成功NPCについて3〜12を反復
15. Concept Exposure
16. Vitality / Aging / Lifecycle更新
17. Birth Queue解決
18. Death cleanup
19. 翌日へ

Observationは原則として日初に一度だけ行い、Micro Roundごとに周囲を完全再観測しない。ただし攻撃を受けた、攻撃が失敗した、交流で情報を受信した、追撃された、対象が死亡した等、本人が直接経験したActionOutcomeは即時にPerceptionへ反映できる。高Action個体が同日中に古い周辺情報で複数行動することは、許容する逸脱要因である。

## v0.15 Micro Round phases

Targeted Action PhaseはAttack、Communication、ReproductionをMove、Flee、Restより先にResolutionする。対象が移動する前に処理してTargetAbsentを減らすためである。

Attack、Communication、Reproduction相互の固定優先順位は未決であり、列挙順や実装都合をBaseline化してはならない。具体順序を必要とする実装は保留する。

Movement PhaseはMoveとFleeを扱い、その後にRestを扱う。各phaseの競合は既存のEffectiveActionとseed付きtie-break規則へ従う。

## Interrupt and intent replacement

Attackを受けたNPCは現在の未実行ActionIntentを破棄し、最新の自己State / Perceptionを使ってUtility AIを1回だけ再評価できる。得たActionは同Micro Roundの元Action枠を置き換え、追加Actionにはならない。同一Micro Roundで複数回AttackされてもAttack由来の再評価は最大1回である。

Reproduction Rejectでは申込対象の未実行Intentを維持する。Acceptでは既存Intentを破棄し、同じAction枠を最大1回だけ再評価する。Rejectを無料の行動キャンセルとして利用できない。

ReactionとIntent置換は追加Micro Round回数を消費せず、既存のReaction非再帰規則を維持する。

## TargetAbsent outcome

Perception上の位置を基準に選んだTargeted ActionのResolution時に、Targetがその位置・距離へ存在しなければTargetAbsent Outcomeを返す。行動者は対象PositionをUnknownまたは無効Confidenceとして即時更新し、次Decisionで同じ古いPositionを根拠に反復できないようにする。

TargetAbsentは対象の死亡や正確な現在位置を自動開示しない。Attack命中/失敗、Communication成立/不成立、Reproduction成立/不成立、Pursuit等も、本人が直接経験したOutcomeとして同日中にPerceptionへ反映できる。

## Additional actions

初回Actionは通常通り可能。以後の参加確率はv0 defaultとして次を使う。

```text
P(repeat) = EffectiveAction / (EffectiveAction + 5)
```

EffectiveAction 0で0%、5で50%、10で約66.7%。ConceptMarkでBase scaleの10を超えることを許容する。1 NPC 1日最大5 Actionとし、上限はConfig化する。追加判定に成功したAlive NPCだけが次Micro Roundへ進む。

## Conflict ordering

同じ資源・Cellを競合するIntentはEffectiveAction値の高いNPCを優先し、完全同値だけseed付き乱数で解決する。Entity配列順、生成順、Dictionary列挙順に依存してはならない。

同一空きCellを選んだMove競合の敗者はUtility AIへ戻らず、残る有効移動先だけを再抽選する。再抽選先がNPC占有CellならCollision Attackへ変換し、候補が尽きればMoveFailedとする。

## Second step

MoveまたはFleeの1マス目の後、次のv0 defaultで2マス目を試みる。

```text
P(secondStep) = Clamp(0.02 * EffectiveAction, 0, 1)
```

通常Moveは原則同方向、Fleeは主観上のThreatからさらに離れる方向を選ぶ。2マス目が不可能なら1マス地点で終了する。

## Determinism baseline

同じCode Version、Config、RunSeedなら同じSimulation Event列を再現可能にする。単一共有乱数列へ全面依存せず、run seedから少なくとも `subsystem / tick / entity / purpose` で用途別streamを派生する。

用途例はUtilityChoice、Mutation、MoveDirection、MoveConflict、CommunicationDistortion、SubjectSwap、CombatHit、CombatDamage、BirthLocationである。無関係な乱数利用追加が既存の全結果をずらさない構造を目標とする。

## Immediate death and end-of-day cleanup

Reality ResolutionでCurrentHPが0以下になった時点から即座にDeadとして扱う。同Tickの新Action、Micro Round、Counterattack、Pursuit、Reproduction、Communicationへ参加できない。Corpseは持たず、Cell占有を即時解除するため、後続Micro Roundの別MoveはそのCellを利用できる。

攻撃者は相手を倒したAttackまたはCollision Attackと同じAction内でそのCellへ移動しない。Tick末Death phaseはDeath Event確定、Lifecycle cleanup、index / collection cleanupを担当する。

## Birth queue arbitration

Reproduction Success時に両親ID、受胎時Position、GeneticData / seed informationをBirthRequestへ固定する。後の親の移動やBirth解決前の死亡でRequestを移動・キャンセルしない。

Tick末には全BirthRequestをまとめ、各Requestが受胎時の両親隣接Cell和集合から有効な希望Cellをseed付きで選ぶ。同一Cell競合はseed付き決定論的tie-breakで1件だけ勝者とし、敗者は残る候補から再抽選する。全候補が尽きた場合だけBirth Failureとなる。queue順、Entity生成順、collection列挙順に依存させない。

Birth解決時点で空いている死亡Cellは利用可能。LandmarkとAlive NPC占有Cellは利用できない。

採用理由は [`ADR-0007`](../decisions/ADR-0007-v0-time-and-micro-rounds.md) と [`ADR-0011`](../decisions/ADR-0011-partitioned-deterministic-rng.md) を参照する。

## Headless invariants

- DecisionはRealityを直接読まない。
- 未観測Reality変更は同じPerceptionとseedのDecisionを変えない。
- Action競合は入力配列順を変えても同じ結果となる。
- Dead NPCは同Tickの後続行動・Reactionへ参加しない。
- Birth競合はqueue順を変えても同じ結果となる。
- Targeted ActionをMove / Flee / Restより先に解決する。
- Attack InterruptとReproduction Accept Interruptは各由来につき同一Micro Round最大1回で、Action枠を増やさない。
- Reproduction Rejectは相手の未実行Intentを維持する。
- TargetAbsent後に同じ古いPositionによるTargeted Actionを反復しない。
- 同一Code、Config、RunSeedから同じEvent列を得る。
- UI render頻度を変えてもSimulation結果は変化しない。
