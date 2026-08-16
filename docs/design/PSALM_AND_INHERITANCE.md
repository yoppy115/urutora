# Psalm and Inheritance

**Status:** Baseline constraints / Draft mechanics

## Baseline: transformation of history

```text
Reality -> Perception -> History -> Psalm
```

HistoryはRealityそのものではない。世界の住人が出来事をどう理解し、語ったかを含む。Psalm / 詩篇は、その歴史と重要なピンから生成され、次世界へ物語として渡る。

## Baseline: dual inheritance

前世界で上位存在の力を継承した人物は、次世界で上位存在になる。

- **上位存在本人**: 前世界での実際の性質、主観、行動傾向を引き継ぐ。
- **詩篇**: 前世界でその人物がどう認識・物語化されたかを引き継ぐ。

本人と詩篇は矛盾してよい。実際は慎重だった人物が「恐怖を知らぬ英雄」と語られた場合、次世界では慎重な本人と勇猛を称える詩篇が同時に存在し、この矛盾が新しい逸脱源になる。

採用理由は [`ADR-0004`](../decisions/ADR-0004-dual-psalm-inheritance.md) を参照する。

## Baseline: revelation and response

```text
Player -> Revelation -> World
World -> Psalm -> Player
```

啓示はNPCへの直接命令ではなく、世界の認識システムへ特殊な情報を入力する行為である。受信者、時代、文化、関係によって異なる解釈と結果を生む。

啓示は希少な介入とする。大きな世界フェーズごとに1回程度という方向性を採用するが、最終回数はDraftとする。

## Baseline: higher entities

上位存在は基本的に能動的な通常NPCではなく、世界を支える杭・概念の核として存在する。

- 移動し続けない。
- 世界を直接支配しない。
- ラスボスに固定しない。
- 信仰だけでなく、嫌悪、恐怖、対立を通じても影響圏を形成し得る。

## Baseline: succession

滅亡・再編フェーズでは上位存在の力の継承を発生させる。

- **簒奪**: 強者が上位存在を倒して力を奪う。
- **授与**: 上位存在本人が後継者を選び、力を譲る。

## Baseline: blessings

重要人物としてピン止めされたNPCは、上位存在から注目され、加護を受ける可能性がある。対象選択は完全ランダムにせず、行動パターン、概念親和性、距離、過去行動、少量の乱数などを利用する。

似た性質だけでなく、真逆の上位存在から加護される可能性も残し、その矛盾を逸脱源にする。

## Baseline: narrative generator boundary

LLMはSimulation Coreに使用しない。機械可読ログ、主観、ピンを読み、人間可読な歴史・詩篇を生成する交換可能なadapterとして扱う。採用理由は [`ADR-0006`](../decisions/ADR-0006-llm-outside-simulation-core.md) を参照する。

## Draft mechanics

- 詩篇生成へ渡すログとピンの選別。
- HistoryとPsalmのデータschema。
- 上位存在候補、簒奪、授与の成立条件。
- 加護対象の各評価要素の重みと効果。
- 啓示の最終回数、伝播、変形規則。
- ローカルLLM、常駐型AI、API、非LLM fallbackの選択。
- 生成結果の検証、失敗時処理、再生成規則。

