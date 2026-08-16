# Concepts and Difficulties

**Status:** Baseline constraints / Draft mechanics

## Baseline: role

概念は世界が獲得した答え・適応であり、困難はその答えによって生まれた脆弱性、またはその答えでは解決しにくい次の問いである。

概念と困難をコードに直書きせず、データとして追加・調整できるようにする。

## Baseline: network structure

概念と困難は1対1に固定しない。

```text
概念A --+
         +-> 困難X -> 適応 -> 概念C
概念B --+
```

複数概念の相互作用が一つの困難を生み、一つの困難への適応が新概念を生み得る。

## Baseline: initial prototype concepts

最初のプロトタイプは3概念から始める。

| Concept | Related base abilities | Meaning |
| --- | --- | --- |
| 闘争 | 行動力、戦闘 | 世界へ積極的に働きかけ、障害を突破する |
| 生存 | 最大HP | 存在し続け、耐える |
| 交流 | コミュニケーション | 他者と情報・意図を交換する |

## v0 Concept Landmarks

3概念を1 Cell占有の固定Landmarkとして置く。NPCはLandmark Cellへ侵入・通過できない。これは通常NPCではなく、世界を支える杭の最小表現である。

v0 defaultではLandmarkからのChebyshev距離に応じて、1日ごとにConcept Exposureを加える。

| Distance | Daily exposure |
| --- | --- |
| 1 | +1.00 |
| 2 | +0.50 |
| 3 | +0.25 |
| 4以上 | 0 |

Exposure 100以上で対応する永久ConceptMarkを取得する。閾値と増加量はConfig。同一Markはstackせず、異種Markは併存できる。ExposureとMarkは遺伝しない。

## v0 ConceptMark effects

- 闘争: `EffectiveAction = BaseAction * 1.2`、`EffectiveCombat = BaseCombat * 1.2`。
- 生存: `EffectiveMaxHP = BaseMaxHP * 1.2`。
- 交流: `EffectiveCommunication = BaseCommunication * 1.2`。

倍率はv0 configurable。Base遺伝値を書き換えない。Mark自体は遺伝しないが、生存、戦闘、行動、情報交換、繁殖機会へ影響し、その個体のBase形質を次世代へ残しやすくすることで淘汰環境を歪め得る。

採用理由は [`ADR-0012`](../decisions/ADR-0012-concept-landmarks-and-selection.md) を参照する。

## Draft: initial difficulties

現時点の有力候補は次の通りだが、最終名称ではない。

| Concept | Difficulty candidate |
| --- | --- |
| 闘争 | 暴戻 |
| 生存 | 疾病 |
| 交流 | 不和 |

概念・困難名は原則として漢字2文字を目標にし、必要なら漢籍、古語、造語を用いる。名称はデータの安定IDと分離する。

## Baseline: world evolution

- 複数の上位存在の影響圏を統合した社会が覇権を得た場合、入力概念の機械的な合成ではなく、その社会の実際の成功原理から新概念を生成する。
- 長期安定した世界では、成功を分析し、その成功では簡単に解けない新しい困難または概念を次世界へ追加する。
- 新困難の設計では、人類史や現代社会の表面設定をコピーせず、問題構造を抽象化して利用してよい。

例: SNSを直接再現するのではなく、「情報伝播速度が真偽検証速度を上回る」という困難として扱う。

## Draft mechanics

- 共通schema、型、schema version。
- v0 Landmark以外で概念・困難がNPC、世界、啓示へ作用する方法。
- 競合、合成、進化の計算方法。
- 新概念・新困難の生成アルゴリズム。
- LLMを用いる場合の出力検証と安定ID付与。

実データは将来 `simulation/concepts/` に置く。
