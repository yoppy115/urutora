# ADR-0005: Heredity is limited to base abilities and adopted Utility coefficients

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

基礎能力から自然に生じる行動傾向、経験、文化、現在状態まで独立した遺伝子にすると、意味の重複と説明困難な進化が生じる。

一方、Utility評価係数は、同じ能力と状況でも行動評価が異なる個体差を作る。

## Decision

遺伝対象を次のカテゴリーに限定する。

### Base abilities

- 最大HP
- 行動力
- 戦闘
- コミュニケーション

### Utility evaluation coefficients

- 危険選好
- その他の係数は、Utilityモデルで正式採用された場合だけ追加できる

評価係数というカテゴリーはBaselineだが、危険選好以外の具体的な係数群は未決である。

現在HP、年齢、記憶、Perception、噂、経験、文化、学習結果、現在の関係、派生説明ラベルは遺伝しない。

## Reasons

- 能力と行動評価を分離する。
- 基礎能力と派生性質の二重遺伝を避ける。
- 個体進化と社会・文化進化を区別する。
- save schemaと実験結果を説明可能にする。

## Consequences

- 遺伝schemaはallowlistで検証する必要がある。
- 新しい評価係数の遺伝化は、Utilityモデルへの採用と同時に文書更新が必要になる。
- 交叉、変異、値域はDraftとして別途設計する。
- 寿命と老化はReproductionから分離して扱う。

## Rejected alternatives

### Inherit every personality or behavior label

能力、評価、経験、文化の境界が失われ、重複する遺伝要因が増える。

