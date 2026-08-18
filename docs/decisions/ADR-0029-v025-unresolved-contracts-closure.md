# ADR-0029: close v0.2.5 knowledge, fission, migration, and phase contracts

- **Status:** Accepted
- **Date:** 2026-08-18
- **Amends:** ADR-0025、ADR-0026、ADR-0027

## Context

v0.2.5は三種Knowledge、有限Person Memory、Fission、Migration、増分Statisticsを採用したが、EventBeliefへ残すEvent、SettlementBeliefの直接取得、eviction用ConfidenceとUnknown位置、Fission Center、Migration到着、Struggleの現Version上の扱いが未確定だった。このままでは実装が独自のImportance式、Reality参照、配列順、曖昧な「到着」を作り得る。

## Decision

EventBelief候補は、WorldPhaseTransition、SettlementFormation、SettlementRenewal、SettlementFission、SettlementDissolution、SettlementIntegration、InvasionStarted、InvasionEnded、Conquest、ConceptMarkAcquiredのMandatory Event、または既存`PinImportance >= 60`のEventに限定する。候補でも、直接経験、直接Observation、Communication、所属・参加による正式通知のいずれかでNPCが認識した場合だけ保存する。Importanceを持たない非Mandatory Eventへ汎用式を発明しない。

SettlementBeliefの入口は、自身のActive Affiliation、距離3以内のCenter直接Observation、所属NPCのAffiliation表示、関連Eventへの直接参加、Communication、本人へ適用されたSettlement ActionOutcomeに限定する。自己所属はId、Active、Center、本人が知るRelationを正確に持てるが、人口、住民一覧、Friction等を全知しない。PopulationEstimateはCommunication、人口を明示するMemorable Event、将来の専用観測契約からだけ更新する。

Person eviction用`AggregatePersonConfidence`はAliveStatus、Position、EstimatedHP、EstimatedCombat、LifeStage、SettlementAffiliation、ConceptMarksの7 fieldを分母とし、Unknownを0として平均する。これはeviction専用である。保護規則後の順位はHearsayOnly、古いLastRecognizedTick、低AggregatePersonConfidence、遠いLastKnownPosition、stable SubjectIdである。Position Unknownは`PositiveInfinity`、Knownは自己現在位置とのChebyshev距離とする。

Fission CenterはFission判定開始時のimmutable snapshotから選ぶ。Map内、通常占有可能、非Landmark、距離8～24、親子Core非重複、非親Settlement Core / Influence非重複等を満たすCellを対象とし、直近30日のCell別Unaffiliated Resident-Days、現在Unaffiliated在住、5×5幾何中心へのChebyshev距離、named seedの順で決定する。CenterはdesignationなのでNPC占有Cellも候補にできる。候補がなければそのhotspotを不成立とし、同じMaintenanceの次候補を評価できる。

MigrationはAliveでchild SettlementがActiveかつ現在位置がchild Influence radius内に入った時点で完了する。Fission成立時にすでに範囲内なら即時完了する。Move、Flee等はAction全体の最終位置で一度だけ判定し、Tick末にもfallback確認する。完了はBias / Active stateを解除して一度だけ記録し、Affiliationを再変更しない。死亡・child無効化では中断し、Invasion中はMigration Biasを一時停止して終了後に必要なら再開する。

v0.2.5の正式WorldPhaseはGenerationとOrderだけである。Expansionはread-only StatisticsでありRuleやBonusを解禁しない。Struggleはv0.2.5 Runで複数Settlement、親子独立存続、InvasionのCombat・死傷・征服、先行者独占、Pressure変換、継続競争を評価した後に別途設計する。

## Consequences

- Mandatory Event名、認識経路、Settlement field取得、Person eviction、Center、Migrationをnamed contractとheadless testで固定する。
- `60`とchild Influence radiusの現行`7`はv0.2.5 configurable defaultであり、普遍法則へ昇格させない。
- 汎用Pin Importance式、Event / Settlement Beliefのcapacity・TTL、Settlement人口の直接観測、Struggle遷移と固有RuleはBacklogへ残す。
- 本ADRはv0.2.5の未決実装境界を閉じるが、ゲームコードが実装済みであることを意味しない。
