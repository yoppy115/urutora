# Perception

**Status:** Baseline constraints / Draft mechanics

## Baseline: Reality and subjectivity

Reality（世界の客観状態）とNPC Perception（NPCが知っている、または正しいと考える主観状態）を別のデータ層として扱う。

NPCはRealityを直接知ることができない。意思決定はPerceptionだけを使い、隠されたRealityを参照しない。

## Baseline: sources of subjectivity

主観には、少なくとも次の要素が影響し得る。

- 観測
- 記憶
- 噂、伝聞、他者からの情報
- 誤認
- 過去経験
- 関係
- 文化的解釈

同じ事実を、NPCごとに異なる意味として解釈できる。

## Baseline: decision and resolution boundary

```text
Reality
  -> Observation
  -> NPC Perception
  -> PerceivedActionCandidate
  -> Utility evaluation
  -> ActionIntent
  -> Reality-side resolution
  -> ActionOutcome
  -> observable facts
  -> NPC Perception update
```

- Utility評価器へ渡せるのはPerception由来の情報だけとする。
- NPCは「できると思う行動」を候補にできる。
- Reality側はActionIntentを権威的に検証し、行動を失敗させてもよい。
- 隠れたRealityを使って、意思決定前に候補を不自然に除外しない。
- 失敗を含む結果は、観測可能な事実を経由して後のPerceptionへ反映する。

採用理由は [`ADR-0002`](../decisions/ADR-0002-subjective-decision-boundary.md) を参照する。

## Baseline: player information

プレイヤーも通常はReality全知ではない。現在の表層状態を中心に観測し、真の原因、完全な内面、全履歴を掘り下げることを主目的にしない。詳細は [`PLAYER_OBSERVATION.md`](PLAYER_OBSERVATION.md) を参照する。

## Draft mechanics

- 視界、距離、記憶容量、忘却の表現。
- 認識誤差と情報遅延の生成規則。
- 矛盾する記憶・噂の統合方法。
- NPC間で情報が伝播する際の変形。
- 文化的解釈を保持するデータ構造。
- プレイヤーへ不確実性を見せる具体的UI。

## Minimum invariants

- NPCが観測していないReality変更で、そのNPCのUtilityが変化しない。
- 同じPerceptionとseedから同じ意思決定が得られる。
- RealityオブジェクトをUtility評価器へ直接渡せない。
- 同じPerceptionなら、隠れたRealityの違いは選択前の候補順位を変えず、ActionOutcomeだけを変え得る。

