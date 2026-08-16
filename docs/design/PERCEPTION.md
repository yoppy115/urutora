# Perception

**Status:** Baseline

## Principle

Reality（世界の客観状態）とNPC Perception（NPCが知っていると考える主観状態）を、別のデータ層として扱う。

NPCの判断はPerceptionだけを使い、隠されたRealityを直接参照しない。誤認・遅延・不完全な情報を導入する場合も、この境界内で表現する。

## Boundary

```text
Reality
  -> observation process
  -> NPC Perception
  -> Utility evaluation
  -> selected action
  -> Reality update
```

- RealityからPerceptionへの変換は明示的な処理にする。
- Utility評価器はRealityの参照を受け取れないinterfaceにする。
- 表示層へ内部数値を自動公開しない。プレイヤーが何を観測できるかは別途設計する。
- デバッグ時の完全状態表示と、製品UIの情報公開は分ける。

## Not decided yet

- 視界、距離、記憶、噂、伝聞をどう表現するか。
- 認識誤差と情報遅延の生成規則。
- Perceptionの忘却、更新、矛盾解消。
- NPC間で情報が伝播する際の変形。
- プレイヤーが得られる情報と、その不確実性の見せ方。

## Minimum tests

- NPCが観測していないReality変更で、そのNPCのUtilityが変化しない。
- 同じPerceptionとseedから同じ意思決定が得られる。
- RealityオブジェクトをUtility評価器へ直接渡せない構造になっている。
