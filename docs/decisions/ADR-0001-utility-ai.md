# ADR-0001: Utility AI uses weighted stochastic selection

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

最大Utilityの行動を必ず選ぶ方式ではNPCの挙動が機械的になり、完全ランダムでは状況と行動の因果性が弱くなる。

## Decision

NPCがPerception上で実行可能だと考える行動をUtilityで順位付けする。候補0件はIdle、1件は確定、2件は両方、3件以上は上位3件を抽選対象とする。

意思決定にはNPCの主観情報だけを使う。選択後のActionIntentはReality側で解決し、失敗し得る。

v0 defaultでは数値安定化したsoftmax `exp((utility - maxUtility) / temperature)` を使い、temperatureをConfig化する。上位候補から確率選択する境界はBaseline、softmaxとtemperature値は交換可能なv0 mechanismである。

## Reasons

- 高Utility行動を選びやすい合理性を維持する。
- 小さな逸脱を発生させ、相互作用による予測不能性の種にする。
- 完全決定論と完全ランダムの両極を避ける。

## Consequences

- 選択に使う乱数seedを保存し、挙動を再現可能にする必要がある。
- 同じ初期状態、設定、seedから同じ選択列になるテストが必要になる。
- 負値、ゼロ、同点とtemperature不正値に対する明示的規則とテストが必要になる。
- 候補、Utility、導出重み、選択結果を診断可能にする。

## Rejected alternatives

### Always choose the maximum Utility

理解は容易だが、反復観測によってNPCが予測されやすい。

### Choose randomly from every action

逸脱は増えるが、状況と行動の因果が薄くなる。
