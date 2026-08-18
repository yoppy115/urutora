# Knowledge and Memory

- **Status:** Baseline / v0.2.5 configurable defaults
- **Decision:** [`ADR-0025`](../decisions/ADR-0025-structured-knowledge-and-person-memory.md)

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
3. 保持fieldのConfidenceが低い人物。
4. 最後に知ったPositionが遠い人物。
5. stable `SubjectId`順。

保護対象だけでcapacityを超える場合も同じ順位で忘れてよい。新しいrecord自身が直ちにevictされてもよい。能力・Mark変化でcapacityが縮小した場合も同じ処理を使う。複数fieldのConfidence集約とPosition Unknownの順位は未決である。

旧Subject + Propertyごと3件FIFOは現行仕様ではない。PersonBeliefは各fieldの現在採用値とprovenanceだけを持つ。

## EventBelief

通常の移動、会話、field転送、Collision抑制等を無制限に記憶しない。次のMemorable Event / PinだけをEventBeliefにできる。

- World Phase。
- Settlement形成、Renewal、Dissolution、Integration、Fission。
- Invasion開始・終了、Conquest。
- ConceptMark取得、重要人物の死亡、大規模人口変動。
- 本人が直接経験した重大Event、Pin。

```text
EventId / EventType / Tick
KnownParticipants / KnownSettlements / KnownLocation / KnownOutcome
Importance / Confidence / SourceType / SourceId
```

各値はUnknownを許す。v0.2.5ではNPC死亡まで保持し、capacity / TTLを設けない。Memorable判定とImportance閾値は未決であり、全raw eventをEventBeliefへ複製してはならない。

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
