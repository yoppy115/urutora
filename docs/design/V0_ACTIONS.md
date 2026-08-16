# v0 Actions and Reactions

**Status:** Baseline boundaries / v0 default and configurable mechanics

すべての候補は主観情報から作り、ActionIntent確定後だけRealityで成立判定する。調整式の係数はv0 Configであり、普遍的な世界法則ではない。

## Ranges and space

距離はChebyshev距離。ObservationとFlee用Threat探索は3、Communicationは2、AttackとReproductionは1。NPCとLandmarkは通過できない。

## Move and Collision Attack

MoveはMap内のLandmarkではない隣接Cellを候補とし、NPC占有Cellを除外しない。方向はv0ではseed付きランダム。

- Empty: 通常Move。
- NPC occupied: Move IntentをCollision Attackへ変換し、通常Attack Resolutionを使う。
- Landmark: 無効。

Collision Attackでは移動をキャンセルする。対象が死亡しても同じAction内ではそのCellへ進入せず、後続Micro Round等で新しいMoveが必要となる。これはv0で最初の暴力を生じさせる主要機構である。採用理由は [`ADR-0008`](../decisions/ADR-0008-move-collision-combat.md) を参照する。

## Rest

Restは主観上、休息欲求を満たす行動である。v0 defaultではRest -4、Activity +1。HPを直接回復せず、自然回復はVitalityが担当する。位置等のRealityは変えない。

## Communication

距離2以内のAがBへCommunicationを選ぶと、A→BとB→Aの双方向で情報を交換する。Communication Needが3減るのは選択したAだけ。各送信者は自身のHeld Informationからseed付きランダムで選ぶ。

```text
sendCount = 1 + floor(EffectiveCommunication / 3)
ErrorMax = 0.10 * (1 - ReceiverEffectiveCommunication / 10)
P(SubjectSwap) = 0.03 * (1 - ReceiverEffectiveCommunication / 10)
```

sendCountは0〜2で1件、3〜5で2件、6〜8で3件、9〜10で4件。数値誤差は `[-ErrorMax,+ErrorMax]`。Subject取り違えは別判定で、受信者が既に認識する合理的なSubjectから置換し、未知Reality Entityを生成しない。将来、重要度、感情、利害、文化、虚偽で選択を歪められる構造にする。

## Attack

明示的Attack Candidateは原則、距離1以内の既知のPerceivedThreatに対してだけ生成する。Collision AttackはMove Resolutionから直接発生する。

```text
P(hit) = Clamp(0.70 + 0.03 * (Combat_attacker - Combat_defender), 0.40, 0.95)

Damage = max(1, 8 + 1.8 * Combat_attacker - 0.8 * Combat_defender)
         * Random(0.9, 1.1)
```

Reality ResolutionではEffectiveCombatを使う。Utility側ではPerception上の推定値から主観命中率と主観Damageを計算し、現実結果と一致しなくてよい。

## Counterattack

攻撃を受けたNPCがAliveかつ攻撃者と距離1なら1回だけ反撃できる。`ReactionCombat = EffectiveCombat * 0.5` として通常Attack式を使う。CounterattackからCounterattackを発生させない。

## Flee and Pursuit

Flee Candidateは距離3以内にPerceivedThreatがある場合に生成できる。主観上のThreatとのChebyshev距離を最大化し、同率をseed付き乱数で選ぶ。1マス後にAction由来の2マス目判定を行う。

Flee後、元のThreatはPursueとDisengageをsoftmaxで評価できる。

```text
R_pursuit = Clamp(
  5 + 0.5 * (PerceivedCombat_target - PerceivedCombat_self)
    + 5 * (1 - HPRatio_self),
  0, 10)

U_pursue = 0.5 * U_attack
          + 0.20 * Need_activity
          - 0.25 * Need_rest
          - (1 - RiskPreference) * R_pursuit

U_disengage = 0.25 * Need_rest
            + (1 - RiskPreference) * R_pursuit

P(pursuit) = Clamp(
  0.50 + 0.05 * (Action_pursuer - Action_fleeing),
  0.20, 0.80)
```

Pursue選択後に成立判定し、成功時だけ通常Attack Resolutionを1回行う。Pursuit AttackからCounterattack、新Flee、新Pursuitを発生させず、Reactionの無限再帰を禁止する。

## Required events and tests

Move、MoveFailed、Communication、Attack、CollisionAttack、Counterattack、Flee、Pursuitを構造化Eventとして識別する。Move Collision変換、CounterattackとPursuitの非再帰、通信がHeld Information外を生成しないこと、誤差上限とSubjectSwap率をheadless testで検証する。
