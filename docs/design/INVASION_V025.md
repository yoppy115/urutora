# Invasion Field Model

- **Status:** Baseline / v0.2.6 configurable defaults
- **Decision:** [`ADR-0028`](../decisions/ADR-0028-invasion-field-rest-and-sustained-victory.md)
- **Current amendment:** [`ADR-0030`](../decisions/ADR-0030-v026-fission-and-invasion-throughput.md)

## Participant state

Invasion参加者は次の排他的stateを持つ。

- `Advancing`
- `Defending`
- `FieldRest`
- `Retreating`
- `Dead`

通常Utility AIとRest Candidate閾値は維持し、Invasion専用Utility AIを作らない。

## Rest, Flee, and retreat

HP比が20%を超える参加者がRestを選んだ場合、その日は`FieldRest`となる。通常Rest効果を受け、そのtickはAdvance / Defense Biasを使わないが、Participantのままである。翌tickにAliveかつEvent継続なら元の役割へ戻る。

`CurrentHP / EffectiveMaxHP <= 0.20`の参加者がRestまたはFleeを選ぶと`Retreating`になる。ParticipantとInvasion Biasを解除し、強いHome Biasを持つ。同じInvasion Eventへ再参加しないが、将来の別Eventには参加できる。

HP比20%超でのFleeはParticipantを維持し、翌tickに役割へ戻れる。Rest / Flee以外は離脱理由にならない。

## Threat and movement

Event開始時、認識可能範囲にいる敵ParticipantをActive PerceivedThreatとして扱える。Observation、Combat、Communicationにより更新する。Event終了後は通常のThreat Memory TTLへ戻す。

Participantの通常MoveではInvasion BiasをHome / Foreign / Migrationより優先する。Fleeは常に上書きでき、Concept Aura Cohesionは副次的に合成する。

- Advancing: 敵Settlementの利用可能Core cellのうち最寄りを目標に近づく。Center固定ではない。敵が近ければ交戦方向を優先できる。
- Defending: Defense Influence内の最寄り侵入者、いなければ決定論的なInvasion frontへ向かう。防衛域から過剰追跡しない。

front cellの具体的決定アルゴリズムは、seed / collection順非依存を守る技術裁量とする。

正式に敵対するParticipant間のCollisionはCombatへ変換する。同Settlement suppressionや平時の親子非侵略は、同一Invasion Eventで敵対する関係を上書きしない。ただし直接親子Settlementは通常targetにできない。

## Victory

### Attack Victory

攻撃側がDefenderのusable Core cellの50%以上を3日連続占有した場合に成立する。50%未満へ戻った日はcounterを0へresetする。Center単独の到達・占有・保持は勝利条件ではない。

### Defense Victory

次のいずれかで成立する。

1. `AliveNonRetreatingAttackParticipants / InitialAttackForce <= 0.30`を3日連続。
2. Defender Influence内のAlive Non-Retreating attackerが0人の状態を`7 + Ceil(Invasion開始時Center間Chebyshev距離 * 1.0)`日連続。
3. Attack VictoryなしでEvent開始から90日経過。

FieldRestはAlive Non-Retreatingとして数える。RetreatingとDeadは数えない。各日条件を外れた連続counterは0へ戻す。

## End of event

勝敗成立時にParticipant state、Invasion Bias、Event専用Threat / lock、連続counterを解除し、終了・Conquest等のEventを発行する。Retreatingへ付いたHome Biasは帰還のため維持できる。

trigger、target評価、Pressure、Friction、Alive-only integration等は、[`SETTLEMENT_FISSION.md`](SETTLEMENT_FISSION.md)のFission先行gateと親子target除外を加えた上で維持する。Damage、Hit、Counterattack、Pursuitも変更しない。

v0.2.6ではTarget Forceを既存Pressure rateの2.0倍（所属人口上限）とし、armed / re-armを廃止して攻撃Settlementごとの開始間隔60日へ置換する。式と不変条件は[`V0_2_6_FISSION_INVASION_THROUGHPUT.md`](V0_2_6_FISSION_INVASION_THROUGHPUT.md)を正本とする。

死亡数quotaは勝敗条件にしない。
