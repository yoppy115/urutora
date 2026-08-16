# ADR-0001: Utility AI uses weighted stochastic selection

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

最大Utilityの行動を必ず選ぶ方式ではNPCの挙動が機械的になり、完全ランダムでは状況と行動の因果性が弱くなる。

## Decision

NPCがPerception上で実行可能だと考える行動をUtilityで順位付けし、上位2〜3候補を取り出す。その候補内で、Utilityから導出した非負の重みにより確率的に1行動を選択する。

意思決定にはNPCの主観情報だけを使う。選択後のActionIntentはReality側で解決し、失敗し得る。

このADRは、Utilityの生値をそのまま確率重みにすることや、softmax等の具体的変換を決定しない。

## Reasons

- 高Utility行動を選びやすい合理性を維持する。
- 小さな逸脱を発生させ、相互作用による予測不能性の種にする。
- 完全決定論と完全ランダムの両極を避ける。

## Consequences

- 選択に使う乱数seedを保存し、挙動を再現可能にする必要がある。
- 同じ初期状態、設定、seedから同じ選択列になるテストが必要になる。
- Utilityから非負重みへの変換、負値、ゼロ、同点、候補不足、候補なしを実装前に別途決める。
- 候補、Utility、導出重み、選択結果を診断可能にする。

## Rejected alternatives

### Always choose the maximum Utility

理解は容易だが、反復観測によってNPCが予測されやすい。

### Choose randomly from every action

逸脱は増えるが、状況と行動の因果が薄くなる。

