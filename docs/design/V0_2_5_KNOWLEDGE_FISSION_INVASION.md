# v0.2.5 Knowledge, Fission & Invasion Update

- **Status:** Baseline / v0.2.5 configurable defaults
- **Supersedes in part:** v0.15のHeld Information、v0.2.4のRest候補、SettlementSupport、Pressure起因Invasion、Invasion離脱・勝敗
- **Implementation:** 未着手。本書はゲームコードを変更しない正史更新である。

## 目的

v0.2.5は、個体の知識を有限で説明可能な主観記録へ再構成し、Settlementの維持を累積Support、拡張を平和的Fission、戦争を持続する前線として接続する。

主な検証対象は次の通り。

- Rest Needが低い個体がRestを選び続けないこと。
- 人物・出来事・Settlementの知識が別の寿命と容量を持つこと。
- SettlementPressureがまず平和的分裂を試し、失敗時だけInvasionへ進むこと。
- Invasionが一度のCenter到達ではなく、前線・休息・撤退・継続占領で決着すること。
- UI、統計、将来のHistory / PsalmがSimulation結果を変えず、因果を追えること。

StruggleはまだWorld Phaseではない。v0.2.5ではFission、Invasion、Settlement数、Pressure等を将来の移行判断に使える観測値として蓄積するだけである。

## 正本

- 知識とCommunication: [`KNOWLEDGE_MEMORY.md`](KNOWLEDGE_MEMORY.md)
- Event保持と統計: [`EVENT_HISTORY.md`](EVENT_HISTORY.md)
- Support、Renewal、Fission: [`SETTLEMENT_FISSION.md`](SETTLEMENT_FISSION.md)
- Invasionの参加状態・移動・勝敗: [`INVASION_V025.md`](INVASION_V025.md)
- 既存のSettlement / Order規則: [`V0_2_SETTLEMENT_ORDER.md`](V0_2_SETTLEMENT_ORDER.md)

同一項目が旧文書と衝突する場合は、本書と上記v0.2.5正本を優先する。明示的に変更していないv0.15～v0.2.4の正史は維持する。

## Rest Candidate

`RestPressure <= 0`、すなわち現行式で`RestNeed <= 2`ならRest Candidateを生成しない。`RestPressure > 0`のときだけ既存式を使う。

```text
U_rest = RestPressure - 0.25 * ActivityNeed
```

この規則は通常時とInvasion参加中の双方へ適用する。RestのNeed効果、Settlement Bonus、Action別疲労は変更しない。

## v0.2.5で維持するもの

- C# / .NET、headless Core / Desktop App分離、1 Tick = 1日。
- Reality / Perception分離、Targeted Action順、ActionOutcomeによる同日更新。
- Base / Effective能力、遺伝、Concept Landmark / Mark / Aura。
- v0.2.4のRest v2、Home / Foreign Bias、Proto-Order、SettlementPressure、Friction、Mobilizationのうち、本書が置換しない規則。
- Settlement形成、Affiliation、Order、自然消滅、征服統合の責務境界。
- Seed決定性、collection順非依存、Presentation非権威性。

## Baseline

- Rest Candidateは`RestPressure > 0`の場合だけ生成する。
- PersonBelief、EventBelief、SettlementBeliefを別categoryとして保持する。
- PersonBeliefは全人物横断capacityとTTLを持ち、field単位の出所・Confidenceを保持する。
- CommunicationはEvent、Settlement、Personの順で「伝える価値がある差分」を送る。
- Recent Event、Incremental Statistics、Historical Milestone、Optional Raw Archiveを分離する。
- SettlementSupportはSupportPotentialから日次累積し、飽和継続はRenewalを発生させる。
- SettlementPressureはまずFissionを試し、有効候補がない場合だけInvasionへ進める。
- 直接の親子Settlement間には、関係が有効な間だけ平時の非侵略規則を置く。
- Invasion参加者はAdvancing / Defending / FieldRest / Retreating / Deadを明示する。
- Attack Victoryは利用可能Coreの50%以上を3日連続占有した場合だけ成立する。
- Defense Victoryは攻撃戦力崩壊、Influence内排除継続、90日経過のいずれかで成立する。

## v0.2.5 configurable defaults

- PersonMemoryCapacity: `DeterministicRound(75 + 15 * StableCommunication)`。
- PersonBelief TTL: 365日。
- SettlementSupport初期値・Renewal後: 50。
- Support増減: `Clamp((SupportPotential - 50) / 50, -1, 1)` / day。
- Support飽和条件: Support 100かつPotential 80以上を365日。
- Fission: Pressure 0.40以上を90日、親距離8～24、5×5 Resident-Days 90以上、現在Unaffiliated 3人以上。
- Fission移住者: Living Affiliated Membersの40%、決定論的丸め、最低4人。
- 子Settlement加入効果: migrant Affinity +10、Core内Unaffiliated +7。
- Attack Victory: usable Core 50%以上を3日。
- Defense Victory: attack force ratio 0.30以下を3日、Influence内0人を7日、または90日。

これらの値は同じConfig / RunSeedで再現可能にし、Run後に調整できる。Baselineの因果と責務境界へ格上げしない。

## 旧仕様の置換

- Subject + Propertyごと最大3件のFIFO記録は、field統合されたPersonBeliefと全人物横断capacityへ置換する。
- 死亡伝聞では人物情報を削除しない規則は、通常のfield更新優先度を通過した`AliveStatus=Dead`でもPersonBeliefを削除できる規則へ置換する。
- Communicationのcategory非優先ランダム送信は、Event > Settlement > Personと差分優先へ置換する。
- `SettlementSupport = 50P + 30R + 20S`という瞬間値は`SupportPotential`へ改名し、累積`SettlementSupport`を別stateとして追加する。
- Pressure 0.65を30日で直接Invasion開始する規則は、Fissionを90日検討した後、有効hotspotがない場合だけInvasionを許す規則へ置換する。
- Invasion中Restによる永久離脱は、通常Restなら1日FieldRest、HP比20%以下のRest / FleeだけRetreatingへ置換する。
- Usable Core 50%の瞬間占有Victoryは3日連続へ置換する。

## 今回実装しないもの

- Struggle Phaseへの正式遷移。
- PersonBeliefの圧縮、EventBelief / SettlementBeliefのcapacityやTTL。
- 国家、宗教、経済、Leader、職業、外交条約、親子Settlement以外の系譜外交。
- Fission以外の植民、複数段階Migration、資源・地形評価。
- Raw Archiveの必須化、History / Psalm本文生成。
- Invasion固有Utility AI、兵科、補給、包囲、城壁。

## 解消済み実装契約

v0.2.5 Unresolved Contracts Closure Patchにより、次を確定した。

- EventBeliefはMandatory Memorable Event、または認識済みでPin Importance 60以上のEventだけを候補とする。
- SettlementBeliefは自己所属、Center / 所属表示の直接Observation、当事者Event、Communication、本人への直接Outcomeから取得する。
- Person evictionは7 tracked fieldのConfidence平均を`AggregatePersonConfidence`とし、Unknown fieldを0として数える。
- evictionでPosition Unknownは`PositiveInfinity`、Knownは自己位置からLastKnownPositionへのChebyshev距離とする。
- Fission Centerは5×5内のCell別Unaffiliated Resident-Days、現在居住、幾何中心距離、named seedの順で選ぶ。
- MigrationはAliveかつActive child SettlementのInfluence radius内へ実到達した時点で完了する。
- Struggleはv0.2.5では意図的に実装しない。Expansion Indicatorsは統計でありWorld Phaseではない。

詳細は [`KNOWLEDGE_MEMORY.md`](KNOWLEDGE_MEMORY.md)、[`EVENT_HISTORY.md`](EVENT_HISTORY.md)、[`SETTLEMENT_FISSION.md`](SETTLEMENT_FISSION.md)、[`WORLD_LIFECYCLE.md`](WORLD_LIFECYCLE.md) を正本とする。

## なお未決の技術・将来仕様

Recent Event Buffer容量、Raw Archiveの分割・圧縮、Invasion front cellの決定論的な具体アルゴリズムは、観測結果を変えない範囲の技術裁量である。

汎用Pin Importance計算式、Event / Settlement Beliefのcapacity・TTL、Settlement人口の直接観測、Struggle正式遷移と固有RuleはBacklogである。
