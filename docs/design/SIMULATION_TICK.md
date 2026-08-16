# Simulation Tick

**Status:** Draft

世界ライフサイクルではなく、シミュレーションの1 tick内で状態を更新する順序を扱う。

## Baseline constraints

- シミュレーションは表示層から独立して進行する。
- 処理順を暗黙のコレクション走査順に依存させない。
- 同じ初期状態、設定、seed、コード版、外部入力から再実行可能にする。
- NPCの意思決定中に、隠れたRealityを参照させない。

## Proposed cycle

次は有力案だが、ADRで採用するまで確定仕様ではない。

```text
immutable Reality snapshot
  -> observation
  -> Perception update
  -> ActionIntent planning
  -> deterministic conflict resolution
  -> atomic Reality commit
  -> ActionOutcome and domain events
  -> logs and presentation projections
```

## Required decisions

1. 時間単位: 固定tick、イベント駆動、または混合方式。
2. Reality更新、知覚更新、意思決定、行動解決、老化の順序。
3. 同一tick内の競合とtie-break規則。
4. NPCごとの意思決定間隔。
5. プレイヤー介入を適用するタイミング。
6. snapshot、save/load、replayの境界。
7. 乱数ストリームの分割方法。

## Definition of ready

実装前に、少なくとも次を具体例付きで決め、ADRへ記録する。

- 1 tickの入力、出力、読み取り可能な状態。
- 同時ActionIntentの競合例と解決結果。
- seed、初期状態、外部入力を保存する形式。
- 10〜100 tickをヘッドレス実行する検証方法。

