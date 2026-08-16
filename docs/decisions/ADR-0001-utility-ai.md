# ADR-0001: Utility AI uses weighted stochastic selection

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

最大Utilityの行動を必ず選ぶ方式では、NPCの挙動が決定論的になり、プレイヤーが行動を容易に予測できる可能性がある。

一方、全候補から無関係に完全ランダムで選ぶ方式では、行動と状況の因果性が弱くなる。

## Decision

実行可能な行動をUtilityで順位付けし、上位2〜3候補を取り出す。その候補内でUtilityを重みとして確率的に1行動を選択する。

候補数は2または3の範囲で設定可能にし、意思決定にはNPCの主観情報だけを使う。

## Reasons

- 高Utility行動を選びやすい合理性を維持する。
- 小さな逸脱を発生させ、相互作用による予測不能性の種にする。
- 完全決定論と完全ランダムの両極を避ける。

## Consequences

- 選択に使う乱数seedを保存し、挙動を再現可能にする必要がある。
- 同じ初期状態・設定・seedで同じ選択列になるテストが必要になる。
- Utilityが0以下、候補不足、同点の場合の扱いを実装前に定義する必要がある。
- 確率選択の結果だけでなく、候補と重みを診断可能にする設計が望ましい。

## Rejected alternatives

### Always choose the maximum Utility

理解は容易だが、反復観測によってNPCが機械的に予測されやすい。

### Choose randomly from every action

逸脱は増えるが、状況と行動の因果が薄くなる。
