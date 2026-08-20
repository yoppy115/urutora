# v0 Actions and Reactions

**Status:** Baseline boundaries / v0.2 default and configurable mechanics

すべての候補は主観情報から作り、ActionIntent確定後だけRealityで成立判定する。調整式の係数はv0 Configであり、普遍的な世界法則ではない。

通常Utility AIはMove、Rest、Communication、Attack、Flee、ReproductionからActionIntentを作る。Counterattack、Pursuit、Reproduction Accept / Rejectは別のReaction Utilityであり、Micro Round追加回数を消費せず、Reactionから同種Reactionを再帰させない。

v0.15ではTargeted ActionをAttack → Reproduction → Communicationの順で、Move、Flee、Restより先にResolutionする。後続phaseは先行phase後のRealityで再Validationする。

Attack Phaseは生成済みAttack Intentを処理し、Dead、Intent Interrupt等を即時反映する。Reproduction PhaseはAttack後のAlive、距離、HP等を再検証する。Communication Phaseは両phase後のAlive、距離、存在を再検証し、成立した場合だけ双方向交換する。

Interruptで再抽選されたIntentは終了済みphaseへ巻き戻さない。現在Micro Roundでまだ未処理の適切なphaseがなければ失効する。

## Ranges and space

距離はChebyshev距離。ObservationとFlee用Threat探索は3、Communicationは2、AttackとReproductionは1。NPCとLandmarkは通過できない。

## Move and Collision Attack

MoveはMap内のLandmarkではない隣接Cellを候補とし、NPC占有Cellを除外しない。方向はv0ではseed付きランダム。

- Empty: 通常Move。
- NPC occupied: Move IntentをCollision Attackへ変換し、通常Attack Resolutionを使う。
- Landmark: 無効。

Collision Attackでは移動をキャンセルする。対象が死亡しても同じAction内ではそのCellへ進入せず、後続Micro Round等で新しいMoveが必要となる。通常Attack Resolutionを使うため、条件を満たす被攻撃者はCounterattackできる。攻撃側はOutcomeから相手EntityId、Position、戦闘結果を知り、被攻撃側は攻撃者をPerceivedThreatへ登録する。これはv0で最初の暴力を生じさせる主要機構である。採用理由は [`ADR-0008`](../decisions/ADR-0008-move-collision-combat.md) を参照する。

v0.2.4以降、Generation中でも同じActive Settlement所属者間のCollision Attackは抑制する。それ以外の治安RuleはOrder境界を維持する。異Settlementの平時Collisionは対称Frictionへ変換し、v0.2 defaultで+1する。Invasion中の敵同士だけCombatへ戻る。

Order中、Settlement所属NPCはInfluence内UnaffiliatedがActive PerceivedThreatでなければExplicit Attack Candidateを生成しない。生成済みIntentもReality ResolutionでUnaffiliated / Influence / Active Threatを再Validationする。UnaffiliatedのThreat行為でThreat Memoryへ登録された後は、期限中のExplicit Attack Candidateを許可する。平時の他Settlement所属者へのExplicit Threat EventはFriction +3 defaultとし、Counterattackで同一事件を二重加算しない。

## Rest

Restは主観上、休息欲求を満たす行動である。Rest -4、Activity +1を維持し、HPを直接回復させない。v0.2.5では`RestNeed <= 2`、すなわち`RestPressure <= 0`ならCandidateを生成せず、それを超える場合だけ既存対数RestPressureをUtilityへ使う。

初回Runで長く見えたため、v0.2 Order中はSettlement CoreのRest Need減少だけを1.5倍し、Activity側の既存効果は変えない。Generation中はBonusなし。Centerからradius 5以内のRest Collisionで未実行Rest Intentを解除し、同一Micro Round最大1回だけ元Action枠を再評価できる。

v0.2.5ではHP比20%超のInvasion ParticipantがRestを選ぶと1日FieldRestとなり、Eventへ残る。HP比20%以下でRestまたはFleeを選んだ場合だけRetreatingとなり、同一Eventへ再参加しない。非重傷FleeはParticipantを維持する。Death、Event終了、Victory、統合によるEvent無効化ではParticipant状態を解除する。詳細は [`INVASION_V025.md`](INVASION_V025.md) を正本とする。

## TargetAbsent

Attack、Communication、Reproduction等でTargetが既知Position / 距離に存在しなければTargetAbsent Outcomeを返す。行動者の対象Position情報をUnknownまたは無効Confidenceへし、同じ古い位置による反復を止める。対象の死亡や現在位置は開示しない。

## Communication

距離2以内のAがBへCommunicationを選ぶと、A→BとB→Aの双方向で情報を交換する。Communication Needが3減るのは選択したAだけ。各送信者は自身のKnowledgeからEvent > Settlement > Person順に、receiverへ伝える価値があるfieldを選ぶ。複数targetは有効候補からseed付きランダムで選び、情報価値はv0 Utilityへ加えない。詳細は [`KNOWLEDGE_MEMORY.md`](KNOWLEDGE_MEMORY.md) を正本とする。

```text
sendCount = 1 + floor(EffectiveCommunication_sender / 3)
CommQuality = Clamp(EffectiveCommunication_receiver, 0, 10)
ErrorMax = 0.10 * (1 - CommQuality / 10)
P(SubjectSwap) = 0.03 * (1 - CommQuality / 10)
```

sendCountはBase scaleの0〜2で1件、3〜5で2件、6〜8で3件、9〜10で4件。ConceptMarkによりEffectiveCommunicationが10を超えた場合は4件超を許容する。一方、品質は10へClampするため、誤差率やSubjectSwap率は負にならない。数値誤差は `[-ErrorMax,+ErrorMax]`。Subject取り違えは別判定で、受信者が既に認識する合理的なSubjectから置換し、未知Reality Entityを生成しない。Confidence伝送は [`PERCEPTION.md`](PERCEPTION.md) に従う。

## Attack

明示的Attack Candidateは原則、距離1以内の既知のPerceivedThreatに対してだけ生成する。Collision AttackはMove Resolutionから直接発生する。

```text
P(hit) = Clamp(
  0.70 + 0.03 * (EffectiveCombat_attacker - EffectiveCombat_defender),
  0.40, 0.95)

Damage = max(
  1,
  4 + 0.9 * EffectiveCombat_attacker - 0.4 * EffectiveCombat_defender)
         * Random(0.9, 1.1)
```

v0.15はBaseMaxHP約50への変更に合わせ、DamageのbaseとCombat係数を旧v0の約0.5倍へ再scaleする。Random(0.9,1.1)とHit Rateは変更しない。

Reality ResolutionではEffectiveCombatを使う。Utility側でもv0.15の新Damage係数を用いて、自身の正確なEffectiveCombatと対象のPerceivedCombat / PerceivedHPから主観命中率、期待Damage、ThreatNeutralization、対象別 `U_attack` を計算する。定義は [`UTILITY_AI.md`](UTILITY_AI.md) を正本とし、現実結果と一致しなくてよい。

## Counterattack

攻撃を受けたNPCがAliveかつ攻撃者と距離1なら1回だけ反撃できる。`ReactionCombat = EffectiveCombat * 0.5` としてv0.15の新Damage式を使う。CounterattackからCounterattackを発生させない。

## Flee and Pursuit

Flee Candidateは距離3以内にPerceivedThreatがある場合に生成できる。複数Threatでは `R_threat` 最大をPrimaryThreatとし、同値はseed付き乱数。PrimaryThreatとのChebyshev距離を最大化し、同率をseed付き乱数で選ぶ。1マス後にEffectiveAction由来の2マス目判定を行う。

Flee後、元のThreatはPursueとDisengageをsoftmaxで評価できる。

```text
R_pursuit = Clamp(
  5 + 0.5 * (PerceivedCombat_target - EffectiveCombat_self)
    + 5 * (1 - SelfHPRatio),
  0, 10)

U_pursue = 0.5 * U_attack
          + 0.20 * Need_activity
          - 0.25 * Need_rest
          - (1 - RiskPreference) * R_pursuit

U_disengage = 0.25 * Need_rest
            + (1 - RiskPreference) * R_pursuit

P(pursuit) = Clamp(
  0.50 + 0.05 * (EffectiveAction_pursuer - EffectiveAction_fleeing),
  0.20, 0.80)
```

Pursue選択後に成立判定し、成功時だけ通常Attack Resolutionを1回行う。Pursuit AttackからCounterattack、新Flee、新Pursuitを発生させず、Reactionの無限再帰を禁止する。

ここで `U_attack` は [`UTILITY_AI.md`](UTILITY_AI.md) が定義する、直前にFleeした対象に対する対象別Attack Utilityである。Pursuitは他のThreatへ対象を切り替えない。

## Active action costs

Move、Communication、Attack、Collision Attack、Flee、Reproduction Attemptは、成功・失敗に関係なく1 Micro Roundを消費した時点でActivity -2.0を受ける。v0.2.4のRest疲労はCommunication`+0.15`、Move`+0.25`、Reproduction Attempt`+0.35`、Attack / Collision Attack`+0.60`、Flee`+0.70`。Collision AttackはMove疲労と二重加算しない。

CounterattackとPursuitは通常Action用Activityを変えないが、身体疲労としてRest`+0.30`と`+0.40`を適用する。Accept / Rejectには通常Action用Need変化を適用しない。自Settlement内Move疲労の軽減はv0.2.4 Settlement仕様に従う。

## Required events and tests

Move、MoveFailed、Communication、Attack、CollisionAttack、Counterattack、Flee、Pursuitを構造化Eventとして識別する。Move Collision変換、CounterattackとPursuitの非再帰、通信がHeld Information外を生成しないこと、誤差上限とSubjectSwap率をheadless testで検証する。

v0.2ではCollisionSuppressed、RestCollisionInterrupt、FrictionChanged、InvasionParticipation / Withdrawalも診断可能にし、Orderの平時・戦時条件をheadless testで固定する。
