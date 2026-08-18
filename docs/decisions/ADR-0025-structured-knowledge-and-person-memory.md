# ADR-0025: split subjective knowledge and bound person memory

- **Status:** Accepted
- **Date:** 2026-08-18
- **Supersedes:** ADR-0015

## Context

Subject + Propertyごと3件をFIFO保持する旧Held Informationは、人物が増えるほど全体容量が無制限に増え、人物・出来事・Settlementで必要な寿命も区別できなかった。また全record送信は、NPCが何を知り、なぜ更新したかを追いにくくした。

## Decision

主観知識をPersonBelief、EventBelief、SettlementBeliefへ分ける。PersonBeliefは人物ごと1 recordとし、fieldごとにUnknown、provenance、Confidence、UpdatedTickを持つ。全人物横断capacityをStableCommunicationから算出し、365日のTTLと保護付き決定論的evictionを適用する。

CommunicationはEvent > Settlement > Personの順で、receiverに欠けるか優るfieldだけを送る。Event / Settlementはv0.2.5ではNPC死亡まで保持し、全raw eventを記憶へ複製しない。

通常の更新優先度を通って`AliveStatus=Dead`が採用されたPersonBeliefは削除できる。これは直接確認だけに限定しないが、新しい直接Aliveを低優先の伝聞で上書きしない。

## Consequences

- 旧3件FIFO、直接死亡確認だけのSubject purge、category非優先送信は現行仕様ではない。
- Unknownと既定値を型で区別し、DecisionへReality値を補完しない。
- capacity / TTL / eviction / field updateをnamed deterministic ruleで実装する。
- EventのMemorable閾値、Confidence集約、Position Unknown時のeviction距離は別途確定が必要である。

