# Logging

**Status:** Baseline constraints / Draft mechanics

## Baseline purpose

世界の再現、デバッグ、ピン抽出、歴史・詩篇生成のため、機械可読なログを保持可能な構造にする。

最低限、次の論理カテゴリを扱う。

- Reality Log
- Perception Log
- Decision Log
- Action Log
- Birth / Death Log
- Revelation Log
- Pin Log
- Inheritance Log

一つのイベントが複数カテゴリから参照されてもよい。ログカテゴリを必ず別ファイルへ分割する必要はない。

v0では少なくとも次の型を区別できる構造化Simulation Eventを持つ。

- Birth、Death
- Move、MoveFailed
- Communication
- Attack、CollisionAttack、Counterattack
- Flee、Pursuit
- ReproductionAttempt、ReproductionSuccess、ReproductionFailure
- ConceptMarkAcquired
- SettlementFormed、SettlementDissolved、SettlementIntegrated
- AffinityChanged、AffiliationChanged
- WorldPhaseChanged
- CollisionSuppressed、SettlementFrictionChanged、InitialHostilityEstablished
- InvasionStarted、InvasionParticipantJoined / Withdrew、InvasionEnded
- SettlementRenewed、SettlementFissionStarted / Completed、MigrationStarted / Arrived / Ended、ParentChildRelationChanged
- PersonBeliefCreated / Updated / Evicted / Expired / RemovedByDeathBelief、KnowledgeFieldTransmitted
- InvasionParticipantStateChanged、InvasionOccupationProgress、InvasionDefenseProgress
- AuraApplied / AuraExpired

Decision DebugとWorld Eventは概念的に分離する。Desktop AppのEvent Logは同じWorld Event streamのread-only projectionを使う。

## Baseline boundaries

- ログは世界の結果を変更しない。
- 生ログすべてをGitへ保存しない。
- 保存価値のあるrunだけ、設定、seed、commit、要約、注目イベントを `research/` に残す。
- ピンと詩篇入力は、元ログの安定IDを参照できるようにする。
- Perception LogはReality Logの単なる複製にしない。
- Event生成や表示頻度がSimulationの乱数消費・結果・処理順を変えない。
- `run.json`、`yearly_stats.csv`、`final_map.txt` 等のファイル出力はv0初期必須要件ではない。将来追加できる構造だけを保つ。
- Action Eventは通常ActionかReactionか、Attempt / Success / Failure、target、Utility内訳参照、ActionOutcomeを区別できる。
- DeathはHP 0以下になったResolution時点を発生tickとして記録し、tick末cleanup時点と混同しない。
- BirthRequest、希望Cell、競合tie-break、再抽選、BirthFailureを同一のstable request IDで追跡できる。
- TargetAbsent、Position invalidation、Interrupt理由、Intent replacement、再評価回数を追跡可能にする。
- Reproduction FailureはReject、Reality precondition failure、TargetAbsent等を機械可読に区別する。ただしNPC向けOutcomeへ非公開precondition値を露出しない。
- PersonBeliefのcapacity / TTL / 保護順位eviction、死亡認知削除、field update採否を理由付きで診断可能にする。EventBelief、SettlementBelief、World Event / History Logの保持とは分離する。
- Targeted Action EventはAttack、Reproduction、Communicationのphase ordinalと、先行phase後の再Validation結果を保持できる。
- Interrupt再抽選Intentが実行、未処理phase待ち、終了済みphaseのため失効のどれになったかを追跡できる。

## v0.15 required run metrics

v0.15 Runでは少なくとも次を集計可能にする。

- Population: 日/年ごとの人口、最低人口、最終人口。
- Death: Combat、Vitality/Aging、その他、平均死亡年齢。
- Reproduction: Attempt、Success、Reject、Reality precondition failure、Failure理由別件数。
- Targeted Action: Attack / Communication / ReproductionのattemptとTargetAbsent。
- Combat: Collision、Explicit Attack、Counterattack、Pursuit Attack、平均Damage。
- Perception: TargetAbsentによるPosition invalidation、Held Information総数、NPCあたり平均/最大。
- Concept: Exposure、ConceptMark取得数。

目的は人口維持値の調整だけでなく、Population、Combat、Reproductionを生む因果を比較可能にすることである。

## v0.2 logging additions

- Settlement Candidate評価はwindow、対象Reproduction Success Event ID、選択Center、排他結果、Founder IDを追跡できる。
- Generation→Order判定はPopulationCV、DemographicImbalance、rolling window、連続成立日数を記録できる。
- Collisionは通常Combat、同Settlement抑制、Unaffiliated保護、異Settlement Friction、Invasion Combatを理由付きで区別する。
- Reproduction FailureはTargetAbsent、Maturity、HP、Cooldown、Distance、Reject、Other Reality Failureへ可能な範囲で分離する。ただしNPC Outcomeへ内部値を漏らさない。
- Invasionはtarget選択理由、SettlementPressure、trigger rejection、mobilization cohort、Bias離脱、同一Event再参加拒否、勝敗条件、統合をstable Event IDで追跡する。
- AuraはConcept、Holder、対象、所属、Invasion Event、適用 / 失効を追跡できる。
- Settlement Maintenanceは12段階のphase ordinal、日中Event、翌Tick state commitを区別する。
- Hotspot arbitrationはimmutable Candidate ID、Reproduction Success count、seed tie-break、棄却理由、選択Centerを追跡する。
- FrictionはPair、raw collision / root threat count、weighted count、Living Population scale、decay前後、daily impulse、宣言時retentionを追跡し、Counterattack二重計上とActive Invasion combat除外を識別できる。
- 一時MaxHP解除ClampはDamage Eventと分離したstate normalizationとして記録可能にする。
- Invasionは利用可能Core Cell分母、攻撃側占有Cell、Rest / Death離脱、Flee中Participantを追跡する。
- v0.2.1～v0.2.3はHotspot 5×5 / Success 3、出生所属の判定経路、ConceptMark表示、Settlement詳細、Friction、NPC履歴をVersion / commit付きで追跡可能にする。
- v0.2.5はRest Candidate抑止、三種Knowledge、Communication category / field差分、SupportPotential / 累積Support / Renewal、Fission / Migration / 親子関係、Invasion participant state / front / 継続counter / Victory reasonを機械可読にする。

Event保持は[`EVENT_HISTORY.md`](../design/EVENT_HISTORY.md)の四層へ分ける。Recent Bufferから消えたPinを失わず、Optional Archiveの有無、buffer容量、flush間隔でSimulation結果を変えない。
- 征服時のAffiliationChangedはAlive NPCだけに発行し、Dead NPCのHistoryを変更しない。
- ログflush間隔はOperations設定でありEvent意味論を変えない。Run identityと保存運用は[`ENGINEERING_REPRODUCIBILITY.md`](ENGINEERING_REPRODUCIBILITY.md)に従う。

必須集計項目は [`STATISTICS.md`](STATISTICS.md) を正本とする。

## Draft mechanics

- JSONL、binary、database等の保存形式。
- schema versionとmigration。
- snapshotとevent logの分担。
- 保存期間、圧縮、sampling、個体数増加時の性能。
- Perceptionや関係情報の記録粒度。
- 詩篇生成へ渡す情報の選別と秘匿境界。
