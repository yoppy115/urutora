# Reproduction

**Status:** Baseline constraints / Draft mechanics

## Baseline: purpose

繁殖は単なる人口増加装置ではなく、子孫を残した性質を次世代へ伝える淘汰圧として扱う。個体の生存だけでなく、繁殖結果が能力と評価係数の分布を変化させる。

NPCが繁殖を選ぶ場合、その判断は他の行動と同じくPerceptionとUtility AIに基づく。

## Baseline: heritable allowlist

遺伝対象は次のカテゴリーに限定する。

### Base abilities

- 最大HP
- 行動力
- 戦闘
- コミュニケーション

### Utility evaluation coefficients

- 危険選好
- その他の係数は、Utilityモデル設計時に採用されたものだけを追加できる

評価係数というカテゴリーはBaselineだが、危険選好以外の具体的な係数群はDraftである。

基礎能力から自然に生じる性質を、別の遺伝パラメータとして重複させない。例えば現時点では、コミュニケーション能力とは別に「社交性」遺伝子を追加しない。

## Baseline: non-heritable state

原則として次は遺伝しない。

- 現在HP、年齢
- 記憶、Perception、噂
- 個人的経験、学習結果
- 文化、現在の関係
- 能力・評価係数から導出された説明ラベル

## Baseline: inheritance and variation

子の基礎能力と採用済みUtility評価係数には、両親由来の値と小さな変異を含める。乱数はseedで再現可能にし、mutation rate等の調整値は設定へ分離する。

遺伝境界の採用理由は [`ADR-0005`](../decisions/ADR-0005-heritable-genotype-scope.md) を参照する。

## Relation to lifespan

繁殖と世代交代を成立させるため、寿命は必須とする。具体的な老化方式は [`LIFECYCLE_AGING.md`](LIFECYCLE_AGING.md) で扱う。

## Draft mechanics

- 繁殖の成立条件、コスト、時間。
- 親の選択と当事者ごとの意思決定。
- 継承、交叉、突然変異の計算方法。
- 危険選好以外に採用する評価係数。
- mutation rate、値の有効範囲、境界処理。
- 個体数制御と世代交代。
- 系譜を保存する範囲と識別子。

## Definition of ready

実装前に、小さな家系を固定seedで再生できる具体例、遺伝allowlist、非遺伝不変条件を定義する。

