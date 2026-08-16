# Lifecycle, Vitality, and Aging

**Status:** Baseline curve shape / v0.15 configurable control-point defaults

## Baseline

繁殖と世代交代のため寿命と自然死可能性を必須とし、Lifecycle / Agingが年齢、自然回復、自然減衰、死亡を所有する。RestはHP回復を所有しない。

v0.15は約3年、約1095 tickの自然寿命scaleを目標にする。これは現実の人間寿命の再現ではなく、短いSimulationで世代交代と遺伝分布変化を観測するためのconfigurable defaultである。

## Superseded v0 mechanism

旧v0の「出生時から30歳まで線形に回復を減らし、その後線形に老化Damageを増やす」単純モデルと、AgingStartAge 30歳、HealAtBirth 0.10、AgingSlope 3.75e-6等のdefaultはv0.15で廃止する。判断履歴は [`ADR-0009`](../decisions/ADR-0009-continuous-vitality-curve.md) と、それを置き換える [`ADR-0014`](../decisions/ADR-0014-short-life-vitality-and-combat-scale.md) に残す。

## v0.15 data-driven curve

DailyVitalChangeを単一の全生涯3次多項式へ固定しない。複数のAge Control Pointを持ち、隣接区間を連続かつ滑らかなCubic interpolationで接続可能なdata-driven curveとする。一部年齢帯の調整を他の年齢帯へ不要に波及させない。

確定する曲線形状は次の通り。

| Age | Shape baseline |
| --- | --- |
| 0〜0.5歳 | 出生直後は比較的脆く、年齢とともに自然回復力が増す |
| 0.5〜1.0歳 | 強い自然回復期 |
| 1.0〜1.5歳 | 回復が徐々に弱まり、1.5歳付近で0へ近づく |
| 1.5〜2.5歳 | 弱い自然HP減衰期 |
| 2.5〜3.0歳 | 弱減衰から強減衰へ滑らかに加速する遷移期 |
| 3.0歳以降 | 強い自然HP減衰期 |

CurrentHPはEffectiveMaxHPを超えない。生存MarkはBaseMaxHPや曲線を変更せずEffectiveMaxHPだけを1.2倍するため、結果として寿命へ影響し得る。

## Lifecycle defaults

- MatureAge: 180日。
- ReproductionCooldown: 90日。
- Natural lifespan target: 約3年前後。
- InitialAge: 180〜700日。
- Initial CurrentHP: EffectiveMaxHP。

すべてv0.15 configurable defaultで、固定1095日死亡ではない。実際の死亡日はBaseMaxHP、ConceptMark、Combat Damage、Vitality Curve等で変化する。

## Configurable control-point values

各Control Pointの具体的DailyVitalChange値はゲーム思想上の固定値ではなく、v0.15実験用Configとする。Codexは保守的な初期値をConfigへ設定してよいが、次の制約をすべて守る。

- curveを連続的にする。
- 確定済みLife Phaseの符号と強弱関係を崩さない。
- BaseMaxHP約50を前提にする。
- 若年個体が軽傷から回復可能にする。
- 1.5歳以降は自然回復させない。
- 3歳前後から自然死を急速に増やす。
- 特定年齢で不連続な大量死を生じさせない。

初期値はSimulation Run後に再調整する。

## Death boundary and future tests

CurrentHPが0以下になった時点で即座にDeadとなり、Cell占有と同Tickの行動資格を失う。Tick末Death phaseはDeath Eventとcollection cleanupを確定する。

curve schemaが複数Control Pointと滑らかな補間を表現できること、Config値が全Phase形状制約を満たすこと、特定年齢で不連続な大量死を起こさないことをテストする。
