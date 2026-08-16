# Utility AI

**Status:** Baseline constraints / Draft mechanics

## Purpose

NPCの行動に合理的な因果を残しながら、常に同じ最大Utility行動だけを選ぶ予測容易性を避ける。

## Baseline: decision pipeline

```text
Reality
  -> Observation
  -> Perception
  -> Needs
  -> PerceivedActionCandidate
  -> Utility evaluation
  -> top 2-3 candidates
  -> weighted stochastic selection
  -> ActionIntent
  -> Reality-side resolution
  -> ActionOutcome
```

上位候補数は2または3の範囲で設定可能にする。最大Utility行動を必ず実行せず、合理性、個体差、小さな逸脱を両立する。

## Baseline: initial needs

- 生存
- 休息
- 活動
- 交流
- 繁殖

## Baseline: capability and evaluation

基礎能力は「何ができるか」を表す。

- 最大HP
- 行動力
- 戦闘
- コミュニケーション

Utility評価係数は「予測した結果をどう評価するか」を表す。評価係数というカテゴリーを遺伝対象にできることはBaselineとする。

具体的にBaselineとして確認されている係数は危険選好だけである。将来割引、損失回避、不確実性回避、他者重視などはUtilityモデル設計時の候補であり、現時点ではDraftとする。

基礎能力から自然に生じる行動傾向を、同じ意味の独立遺伝パラメータとして重複させない。

## Baseline: theoretical direction

Utility評価の構築では、行動経済学と意思決定理論を参考にする。参照点依存、損失回避、将来割引、主観確率、不確実性回避、他者利益評価は候補要素だが、具体的な採用と式はDraftである。

## Information boundary

- 評価器へ渡せるのはPerception由来の主観情報だけとする。
- NPCは「実際に勝てるか」ではなく「自分は勝てると思うか」を評価する。
- Realityの非公開値を参照する抜け道を作らない。
- Reality側の成立判定は選択後のActionIntentに対して行う。

## Randomness and replay

- 選択に使う乱数源は外部から注入可能にする。
- 実行ごとにseedを保存する。
- 同じ初期状態、設定、seed、コード版から同じ選択列を再現できることを目標とする。
- 候補、Utility、Utility由来の重み、選択結果を診断可能にする。

## Draft mechanics

- Utility式、正規化範囲、各理論要素の採否。
- Utilityから非負の選択重みへの変換。softmaxは有力候補だが未採用。
- temperature等の調整値。
- Utilityが0以下、同点、候補不足、候補なしの場合の規則。
- NPCごとの意思決定間隔。
- 危険選好以外の具体的な評価係数。

採用理由は [`ADR-0001`](../decisions/ADR-0001-utility-ai.md) と [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。

