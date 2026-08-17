# Concepts and Difficulties

**Status:** Baseline / v0.2 configurable Landmark and Aura / Draft future concepts

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

## v0.2 Concept Landmarks

3概念を1 Cell占有の固定Landmarkとして置く。NPCはLandmark Cellへ侵入・通過できない。これは通常NPCではなく、世界を支える杭の最小表現である。

v0.2 defaultではLandmarkからのChebyshev距離に応じて、1日ごとにConcept Exposureを加える。

| Distance | Daily exposure |
| --- | --- |
| 1 | +1.00 |
| 2 | +0.50 |
| 3 | +0.25 |
| 4 | +0.125 |
| 5以上 | 0 |

Exposure 100以上で対応する永久ConceptMarkを取得する。閾値と増加量はConfig。同一Markはstackせず、異種Markは併存できる。ExposureとMarkは遺伝しない。

## v0 ConceptMark effects

- 闘争: `EffectiveAction = BaseAction * 1.2`、`EffectiveCombat = BaseCombat * 1.2`。
- 生存: `EffectiveMaxHP = BaseMaxHP * 1.2`。
- 交流: `EffectiveCommunication = BaseCommunication * 1.2`。

倍率はv0 configurable。Base遺伝値を書き換えない。Mark自体は遺伝しないが、生存、戦闘、行動、情報交換、繁殖機会へ影響し、その個体のBase形質を次世代へ残しやすくすることで淘汰環境を歪め得る。

## Base and Effective baseline

遺伝には常にBase値、Simulation上の実能力にはEffective値を使う。

```text
EffectiveAction        = BaseAction        * ConceptModifier
EffectiveCombat        = BaseCombat        * ConceptModifier
EffectiveCommunication = BaseCommunication * ConceptModifier
EffectiveMaxHP         = BaseMaxHP         * ConceptModifier
```

該当MarkなしはModifier 1.0、ありはv0 default 1.2。EffectiveActionはrepeat、Intent競合、second step、pursuitへ使う。EffectiveCombatはRealityのhit、Damage、Counterattackと自己の主観予測へ使う。EffectiveCommunicationはsendCountへそのまま使い、情報品質計算だけ0〜10へClampする。EffectiveMaxHPはSurvivalNeed、SelfHPRatio、繁殖HP条件、CurrentHP上限へ使う。

Mark取得時にCurrentHPの絶対値を維持し、増えたEffectiveMaxHPまで即時補充しない。v0.15の例では50/50の個体が生存Markを得た直後は50/60となり、将来のVitality回復で不足分を回復できる。

採用理由は [`ADR-0012`](../decisions/ADR-0012-concept-landmarks-and-selection.md) を参照する。

## v0.15 status

初回RunのMark取得は約9.71年で8件だったが、Combat大量死、TargetAbsent、繁殖不全を含むためConcept Exposure単体の妥当な評価とはみなさない。v0.15ではExposure Range、Exposure Rate、Threshold、1.2倍Modifierを変更しない。Settlement導入後の局所滞在時間変化を観測してから再評価する。

## v0.2 Concept Aura

v0.2はExposure radiusを3から4へ拡張し、距離4に+0.125/dayを追加する。Threshold 100と本人Mark 1.2は維持する。

Mark Holderはradius 2以内の同一Settlement所属者へ一時Auraを与える。敵・Unaffiliatedには作用せず、同種Auraはstackしない。異種Auraは併存可能で、範囲外では消失する。

- 共通: Rest Need -0.10/day。
- 闘争: EffectiveAction / EffectiveCombat ×1.1。
- 生存: EffectiveMaxHP ×1.1。
- 交流: EffectiveCommunication ×1.1。

Invasion中は現在radius 2以内の同一Event参加者へHolder方向のCohesion Biasを与えるが、Enemy SettlementへのAdvance Biasを上書きしない。複数Holderは最短距離、同距離はseed付き乱数で選ぶ。

Settlement BonusはMarkを直接付与しない。定住・長寿等でExposure蓄積が変わる間接経路だけを許容する。Auraは非遺伝で、Base値を書き換えない。本人Markと同種他者Auraの正確な合成、および一時MaxHP変化時のCurrentHP上限処理は未決である。

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
