# v0.15 Ecology Update

> **Historical baseline:** 本書はv0.15時点の生態系正史を保存する。v0.2.5はHeld Informationの3件FIFOと死亡認知規則をPerson / Event / Settlement Knowledgeへ置換する。現行仕様は [`KNOWLEDGE_MEMORY.md`](KNOWLEDGE_MEMORY.md) を優先する。

**Status:** Baseline boundaries / v0.15 configurable defaults

## Purpose and evidence

初回v0 Runでは、初期200個体が長期的に約30個体まで減少し、観測された死亡238件は全てCombat由来だった。Reproduction Attempt 24,408件に対してSuccessは68件、Attackの約97.45%、Communicationの約73.31%がResolution時TargetAbsentだった。同日中の古いPosition反復、Realityを必要とする繁殖候補、遅い世代交代、無制限に増えるHeld Informationも確認された。

v0.15はTargeted Action、主観境界、世代時間、情報容量、HP/Damage scaleを修正し、Population・Combat・Reproductionの因果を再観測可能にする。この版ではSettlementを含めなかったが、その境界はv0.2の [`V0_2_SETTLEMENT_ORDER.md`](V0_2_SETTLEMENT_ORDER.md) によって後続拡張された。

## Baseline changes

- Attack、Communication、ReproductionをTargeted Action PhaseでMove、Flee、Restより先に解決する。
- Attackを受けたNPCは未実行Intentを捨て、最新の自己State / Perceptionで同一Action枠を最大1回だけ再評価できる。
- Reproduction Rejectは相手のIntentを維持し、Acceptだけが相手のIntentを捨てて最大1回再評価させる。
- TargetAbsentは対象の既知PositionをUnknownまたは無効Confidenceへし、同じ古いPositionを根拠に反復させない。死亡や現在位置は自動開示しない。
- Reproduction Candidateは対象の主観的Alive、距離1、Matureだけを使い、HP、Cooldown、実距離等はReality Resolutionで検証する。
- Held Informationは同一Subject + Propertyにつき最大3件とし、4件目取得時は最古の記録をFIFOで破棄する。
- 自然寿命scaleを約3年へ短縮し、Vitalityをdata-drivenな滑らかなCubic interpolation curveへ変更する。
- BaseMaxHPとDamageを旧v0の約0.5倍へ同時に再scaleする。

## v0 to v0.15 defaults

| Parameter | v0 | v0.15 |
| --- | --- | --- |
| BaseMaxHP center | about 100 | about 50 |
| MatureAge | 12 years | 180 days |
| ReproductionCooldown | 730 days | 90 days |
| ReproductionNeedGain | +0.01/day | +0.04/day |
| ThreatMemoryDuration | 365 days | 90 days |
| InitialAge | 12–29 years | 180–700 days |
| Natural lifespan target | about 50 years | about 3 years / 1095 ticks |
| Vitality | linear recovery then linear decay | control-point cubic interpolation |
| Damage base/coefficients | 8 / 1.8 / -0.8 | 4 / 0.9 / -0.4 |

これらはv0.15 configurable defaultsであり、不変のゲーム思想ではない。1 Tick = 1日、365日 = 1年、InitialPopulation 200、64×64 Map、Hit Rate、ConceptMark、Rest等はv0.15では変更しない。Restの後続変更はv0.2.4正史を優先する。

## Vitality curve shape baseline

- 0〜0.5歳: 出生直後は比較的脆く、自然回復力が増加する。
- 0.5〜1.0歳: 強い自然回復期。
- 1.0〜1.5歳: 回復が徐々に弱まり、1.5歳付近で0へ近づく。
- 1.5〜2.5歳: 弱い自然HP減衰期。
- 2.5〜3.0歳: 弱減衰から強減衰へ滑らかに加速する。
- 3.0歳以降: 強い自然HP減衰期。

複数Age Control Point間を連続かつ滑らかにCubic interpolationし、一部年齢帯の調整が全生涯へ不要に波及しない構造にする。

## Settlement boundary

Settlement生成、所属、Affinity、回復・Rest・Aging・Reproduction Bonus、安全圏、帰巣、Settlement間敵対、Raid、State / Nationはv0.15時点では対象外だった。v0.2はGeneration、Settlement、Order、Invasion、Auraの明示仕様だけを採用し、State / Nation等は引き続き対象外とする。

個体→集落→社会→国家の方向性のうち、Settlementが強い生存・繁殖・社会利益を持ち、高密度からInvasionが生じる最初の段階をv0.2で具体化する。疾病、内部対立、資源、階層、State / Nation等は引き続き将来範囲である。

## Resolved implementation rules

- Vitality Control Pointの具体的DailyVitalChange値はv0.15 configurable defaultとし、Codexが確定済みPhase形状を守る保守的な初期値をConfigへ設定してよい。
- Held Informationの4件目取得時はConfidenceに関係なく最古記録をFIFOで破棄する。
- Subject消滅を直接確認した場合は、そのSubjectのHeld Informationを全削除する。TargetAbsentや死亡伝聞だけでは全削除しない。
- Targeted Action内部順はAttack → Reproduction → Communicationとする。各後続phaseは先行phase後のRealityで再Validationする。
- Interruptで再抽選されたIntentは、現在Micro Roundでまだ未処理の適切なphaseだけで実行できる。終了済みphaseへ巻き戻さず、実行機会がなければ失効する。

採用理由は [`ADR-0013`](../decisions/ADR-0013-targeted-actions-and-interrupts.md)、[`ADR-0014`](../decisions/ADR-0014-short-life-vitality-and-combat-scale.md)、[`ADR-0015`](../decisions/ADR-0015-bounded-held-information.md) を参照する。
