# Knowledge and Memory

- **Status:** Baseline / v0.2.5 configurable defaults
- **Decision:** [`ADR-0025`](../decisions/ADR-0025-structured-knowledge-and-person-memory.md)
- **Closure:** [`ADR-0029`](../decisions/ADR-0029-v025-unresolved-contracts-closure.md)

## Knowledge categories

NPCの知識を次の三categoryへ分ける。

1. `PersonBelief`: 特定人物について現在どう認識しているか。
2. `EventBelief`: 記憶に値する出来事をどう認識しているか。
3. `SettlementBelief`: Settlementの状態・関係をどう認識しているか。

これらはRealityの複製ではない。`Unknown`を0、false、empty collectionと区別し、UIでは`?`等へ表現できる。NPCが自分自身の内部状態を正確に知ってよい既存例外を除き、DecisionはBeliefだけを使う。

## PersonBelief

人物ごとに最大1 recordを持つ。

```text
SubjectId
LastRecognizedTick
EverDirectlyObserved
AliveStatus: Unknown | Alive | Dead
Position
EstimatedHP
EstimatedCombat
LifeStage
SettlementAffiliation
ConceptMarks
```

各fieldは`Value / Unknown`に加えて`SourceType`、`SourceId`、`Confidence`、`UpdatedTick`を持てる。直接Observation時のSubjectIdは正確である。Reality上のAlive、Position、能力等を自動同期しない。

### 生成と認識時刻

PersonBeliefを新規作成できる入口はObservation、Communication、本人の直接ActionOutcome、Threatを直接受けた経験だけである。RealityにEntityが存在するだけでは作らない。

`LastRecognizedTick`は、直接Observation、本人が関わるAction、人物についてのCommunication、人物を識別できるOutcomeを受けたときに更新する。同じ内容を聞いた場合も、その人物を再認識した事実として更新してよい。

### Field update priority

同じfieldの競合は次の固定順で解決する。

1. 直接ObservationはCommunicationより優先する。
2. 同じSourceTypeでは`UpdatedTick`が新しい方を優先する。
3. 同じtickではConfidenceが高い方を優先する。
4. なお同値ならseedに依存しないstable information ID順。

既知の直接Observation fieldを、低優先の伝聞で上書きしない。Communicationは欠けているfield、または上記優先度で優るfieldだけを更新する。

### 死亡と再認識

`AliveStatus=Dead`が上記の通常優先度を通って採用されたら、そのPersonBeliefを削除する。本人の直接確認だけでなく、信頼度・新しさが優先規則を満たすCommunicationでも削除できる。ただし、既存の新しい直接`Alive`を低優先の死亡伝聞で消してはならない。

死亡という出来事はEventBeliefへ残り得る。後に同じSubjectIdを再観測した場合は新しいPersonBeliefを作成する。

### CapacityとTTL

```text
StableCommunication = BaseCommunication * PermanentCommunicationMarkModifier
PersonMemoryCapacity = DeterministicRound(75 + 15 * StableCommunication)
```

Markなしのmodifierは1.0、永久交流Markは1.2。Auraや一時buffはcapacityへ使わない。例は0→75、2→105、5→150、8→195、10→225、12→255。

`LastRecognizedTick`から365日認識されなければPersonBeliefを期限切れにする。この期間はConfigである。

capacity超過時は次を保護対象とする。

- Active PerceivedThreat。
- 自分と同じActive Settlementの人物。
- `EverDirectlyObserved=true`の人物。

保護されない人物を先に、次の安定順で忘れる。

1. 伝聞だけの人物。
2. `LastRecognizedTick`が古い人物。
3. `AggregatePersonConfidence`が低い人物。
4. 最後に知ったPositionが遠い人物。
5. stable `SubjectId`順。

保護対象だけでcapacityを超える場合も同じ順位で忘れてよい。新しいrecord自身が直ちにevictされてもよい。能力・Mark変化でcapacityが縮小した場合も同じ処理を使う。

`AggregatePersonConfidence`はAliveStatus、Position、EstimatedHP、EstimatedCombat、LifeStage、SettlementAffiliation、ConceptMarksの7 fieldを対象とする。SubjectIdとLastRecognizedTickは含めない。

```text
AggregatePersonConfidence
  = Sum(Confidence of each known tracked field) / TrackedFieldCount
```

Unknown fieldはConfidence 0として7件の分母へ含め、結果を0～1へClampする。これは容量超過時の忘却順位だけに使用し、Combat、位置、その他Action Decisionには個別fieldのConfidenceを使う。

eviction用距離は、Position Unknownなら`PositiveInfinity`とする。KnownならNPC自身の現在位置と有効なLastKnownPositionのChebyshev距離を使う。古くてもfieldが有効なら使用し、失効してUnknownになった時点でPositiveInfinityへ移る。保護規則はこの距離順位より先に適用する。

旧Subject + Propertyごと3件FIFOは現行仕様ではない。PersonBeliefは各fieldの現在採用値とprovenanceだけを持つ。

## EventBelief

通常の移動、会話、field転送、Collision抑制等を無制限に記憶しない。EventBelief候補はMandatory Memorable Event、または`PinImportance >= 60`のEventだけである。Importanceは0～100、60はv0.2.5 configurable defaultである。

Mandatory Memorable EventはImportanceに関係なく次とする。

- WorldPhaseTransition。
- SettlementFormation、SettlementRenewal、SettlementFission、SettlementDissolution、SettlementIntegration。
- InvasionStarted、InvasionEnded、Conquest。
- ConceptMarkAcquired。

Mandatory以外では、既存Pin / Importance値が60以上の重要人物死亡、本人が直接経験した重大事件、大規模人口変動、特異なCombat結果等を候補にできる。既存Importanceを持たないEventへ、この契約を理由に新しい汎用採点を行わない。

```text
EventId / EventType / Tick
KnownParticipants / KnownSettlements / KnownLocation / KnownOutcome
Importance / Confidence / SourceType / SourceId
```

各値はUnknownを許す。v0.2.5ではNPC死亡まで保持し、capacity / TTLを設けない。候補Eventでも、NPCが直接経験、直接Observation、Communication、または所属・参加状態による正式通知で認識した場合だけ保存する。Reality発生だけで全NPCへ自動配布しない。

## SettlementBelief

Settlementごとに最大1 recordを持つ。

```text
SettlementId
ActiveStatus
Center
PopulationEstimate
Relation: Unknown | Friendly | Neutral | Hostile | Parent | Child
ParentSettlementId / ChildSettlementIds
KnownConcepts
LastUpdatedTick / Confidence / SourceType / SourceId
```

Unknownを明示する。Dissolution後もNPC死亡までは記憶を残し、`ActiveStatus`を更新できる。v0.2.5ではcapacity / TTLを設けない。直接親子以外のRelationをRealityから自動配布しない。

### SettlementBelief acquisition

SettlementBeliefを取得・更新できる入口は次だけである。

1. 自身のActive Affiliation。
2. Settlement Centerの直接Observation。
3. Settlement所属NPCのAffiliation表示の直接Observation。
4. Settlement関連Eventへの直接参加。
5. Communication。
6. 自身へ直接適用されたSettlement ActionOutcome。

自身のActive SettlementについてはSettlementId、ActiveStatus、Center、自身とのRelation、本人が当事者として知るParent / Child関係を正確に知ってよい。全人口、全住民、Friction、Support等は自己情報ではない。

通常Observation Range内、すなわちChebyshev距離3以内でCenter cellを直接観測した場合、SettlementId、ActiveStatus、Centerを既存距離別Observation Confidenceで更新できる。正確な人口、全住民、Friction、Support、Pressure、Invasion計画は得ない。

人物を直接Observationした際、その人物のSettlementAffiliationをcategory情報として識別できる。SettlementBeliefがなければSettlementIdだけKnownで他fieldがUnknownの部分recordを作れるが、Centerや人口を自動補完しない。

SettlementFormation / Fission / Integration / Dissolution / Renewal、Conquest、InvasionStarted / Endedの直接参加者は、本人へ明示された範囲だけ関連SettlementBeliefを更新できる。Fission migrantは親とchild、Invasion participantは攻撃・防衛Settlement、征服で所属変更されたAlive NPCは統合先を認識できる。

field更新優先度は、自身の所属状態または直接Observation、直接Event参加、Communication、同SourceTypeなら新しい情報、同tickなら高Confidence、stable information IDの順とする。直接観測Centerを低優先の伝聞で上書きしない。

`PopulationEstimate`はCommunication、人口を明示するMemorable Event、将来の専用観測契約からだけ更新する。Center観測や周囲に見える所属NPC数をSettlement総人口へ自動変換しない。

## Communication selection

既存`sendCount`内で、送信categoryは次の固定順とする。

```text
Event > Settlement > Person
```

receiverがrecordを持たない、fieldがUnknown、senderのfieldが新しい・高Confidence、既存内容と衝突する等の場合だけ「伝える価値がある」。送信者が持たない情報は生成しない。record丸ごとではなく、欠けているか優るfieldだけを送る。

category内順位は次の通り。

- Event: 新しい、Importanceが高い、Confidenceが高い、receiverが未認識、stable EventId。
- Settlement: 未知Settlement、Active更新、Relation更新、未知Center、親子関係、人口、Concept、新しさ、Confidence、stable ID。
- Person: 新規取得・更新、Confidence、Active Threat、同Settlement、重要・Pin、直接Observation済み、その他、stable SubjectId。

既存の数値Distortionはfieldへ適用する。SubjectSwapはreceiverが既に知る人物だけを候補とし、EventId / SettlementIdは取り違えない。受信fieldは既存provenanceとConfidence規則を通す。

## Invariants

- Observation、Communication、直接Outcome、Threat経験以外から人物知識を生やさない。
- Unknownを既定値で埋めない。
- Display、memory eviction、record列挙順、Auraの有無でDecisionを密かに変えない。
- Communicationは送信者が保持するfieldだけを転送する。
- Person MemoryとWorld History / Event Logを同じ保存層にしない。
