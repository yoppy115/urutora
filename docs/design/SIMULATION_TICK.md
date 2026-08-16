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
8. Intent競合解決
9. Reality反映
10. Actionによる追加行動判定
11. 成功NPCについて3〜9をMicro Roundとして反復
12. Event確定
13. Concept Exposure
14. Vitality / Aging / Lifecycle更新
15. Birth Queue解決
16. Death処理
17. 翌日へ

Observationは原則として日初に一度だけ行い、Micro Roundごとに周囲を完全再観測しない。ただし攻撃を受けた、攻撃が失敗した、交流で情報を受信した、追撃された、対象が死亡した等、本人が直接経験したActionOutcomeは即時にPerceptionへ反映できる。高Action個体が同日中に古い周辺情報で複数行動することは、許容する逸脱要因である。

## Additional actions

初回Actionは通常通り可能。以後の参加確率はv0 defaultとして次を使う。

```text
P(repeat) = Action / (Action + 5)
```

Action 0で0%、5で50%、10で約66.7%。1 NPC 1日最大5 Actionとし、上限はConfig化する。追加判定に成功したNPCだけが次Micro Roundへ進む。

## Conflict ordering

同じ資源・Cellを競合するIntentはAction値の高いNPCを優先し、完全同値だけseed付き乱数で解決する。Entity配列順、生成順、Dictionary列挙順に依存してはならない。

同一空きCellを選んだMove競合の敗者はUtility AIへ戻らず、残る有効移動先だけを再抽選する。再抽選先がNPC占有CellならCollision Attackへ変換し、候補が尽きればMoveFailedとする。

## Second step

MoveまたはFleeの1マス目の後、次のv0 defaultで2マス目を試みる。

```text
P(secondStep) = 0.02 * Action
```

通常Moveは原則同方向、Fleeは主観上のThreatからさらに離れる方向を選ぶ。2マス目が不可能なら1マス地点で終了する。

## Determinism baseline

同じCode Version、Config、RunSeedなら同じSimulation Event列を再現可能にする。単一共有乱数列へ全面依存せず、run seedから少なくとも `subsystem / tick / entity / purpose` で用途別streamを派生する。

用途例はUtilityChoice、Mutation、MoveDirection、MoveConflict、CommunicationDistortion、SubjectSwap、CombatHit、CombatDamage、BirthLocationである。無関係な乱数利用追加が既存の全結果をずらさない構造を目標とする。

採用理由は [`ADR-0007`](../decisions/ADR-0007-v0-time-and-micro-rounds.md) と [`ADR-0011`](../decisions/ADR-0011-partitioned-deterministic-rng.md) を参照する。

## Headless invariants

- DecisionはRealityを直接読まない。
- 未観測Reality変更は同じPerceptionとseedのDecisionを変えない。
- Action競合は入力配列順を変えても同じ結果となる。
- 同一Code、Config、RunSeedから同じEvent列を得る。
- UI render頻度を変えてもSimulation結果は変化しない。

