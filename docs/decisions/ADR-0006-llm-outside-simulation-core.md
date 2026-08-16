# ADR-0006: LLMs remain outside the authoritative simulation core

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

LLMは歴史や詩篇の人間可読表現を作るのに有用だが、非決定的で、利用環境、モデル、API、費用へ依存する。Simulation Coreの状態遷移をLLMへ委ねると、再現性と交換可能性を失う。

## Decision

LLMをSimulation Coreの権威的な計算へ使用しない。

LLMまたは非LLM fallbackは、機械可読ログ、主観、ピンから歴史・詩篇・説明文を生成するNarrative AdapterとしてSimulation Coreの外側に置く。

Narrative Adapterは世界状態を直接変更せず、ローカルLLM、常駐型AI、API、fallbackへ差し替え可能にする。

## Reasons

- シミュレーションの再現性を保つ。
- 特定モデル、API、契約、費用への依存を避ける。
- 物語表現と世界法則を別々に改善できる。
- LLMを利用できない環境でもSimulation Coreを実行可能にする。

## Consequences

- Narrative Adapter用の読み取り専用入力schemaが必要になる。
- 生成失敗、timeout、不正形式へのfallbackが必要になる。
- 同じログから文章が変わっても、世界の権威的な結果は変化しない。
- 生成文の保存、再生成、version管理はDraftとして設計する。

## Rejected alternatives

### LLM directly decides world state transitions

再現性、検証可能性、交換可能性を損なう。

