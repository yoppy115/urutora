# Utility AI

**Status:** Baseline

## Purpose

NPCの行動に合理的な因果を残しながら、常に同じ最大Utility行動だけを選ぶ予測容易性を避ける。

## Decision pipeline

1. NPC自身の主観情報を取得する。
2. 現在の欲求を評価する。
3. 実行可能な行動候補ごとにUtilityを計算する。
4. Utility上位2〜3候補を取得する。
5. Utilityを重みとして、候補から確率的に1つ選択する。

上位候補数は2または3の範囲で設定可能にする。どちらをdefaultとするかは、設定スキーマ決定時に確定する。

## Information boundary

- 評価器へ渡せるのはPerception層の主観情報だけとする。
- Reality層の非公開値を参照する抜け道を作らない。
- 行動の実行可否判定でも、NPCが知り得ない情報を意思決定理由に混ぜない。

## Randomness and replay

- 選択に使う乱数源は外部から注入可能にする。
- 実行ごとにseedを保存する。
- 同じ初期状態、設定、seed、コード版から同じ選択列を再現できることを目標とする。
- テストではseedを固定し、候補外の行動が選ばれないことを検証する。

## Not decided yet

- Utility式、欲求の種類、正規化範囲。
- Utilityが0以下の場合の重み変換。
- 同点、候補不足、実行可能行動なしの場合の規則。
- NPCごとの意思決定間隔。
- 選択理由をログへどこまで残すか。

採用理由は [`ADR-0001`](../decisions/ADR-0001-utility-ai.md) を参照する。
