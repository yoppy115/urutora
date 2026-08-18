# Settlement Support, Renewal, and Fission

- **Status:** Baseline / v0.2.6 configurable defaults
- **Decision:** [`ADR-0027`](../decisions/ADR-0027-accumulated-support-renewal-and-fission.md)
- **Closure:** [`ADR-0029`](../decisions/ADR-0029-v025-unresolved-contracts-closure.md)
- **Current amendment:** [`ADR-0030`](../decisions/ADR-0030-v026-fission-and-invasion-throughput.md)

## SupportPotential and accumulated support

旧`SettlementSupport = 50P + 30R + 20S`を瞬間的な潜在値`SupportPotential`へ改名する。P / R / Sの既存90日定義と0～1 Clampは変えない。

```text
SupportPotential = 50 * ResidentPresence
                 + 30 * ReproductionContinuity
                 + 20 * SocialActivity

DailySupportDelta = Clamp((SupportPotential - 50) / 50, -1, 1)
SettlementSupport = Clamp(SettlementSupport + DailySupportDelta, 0, 100)
```

新規SettlementとRenewal直後の`SettlementSupport`は50。SupportPotentialが100なら+1/day、75なら+0.5/day、50なら0、25なら-0.5/day、0なら-1/dayとなる。

自然消滅の既存hysteresisは累積Supportへ適用する。

- Support < 25: `LowSupportDays++`。
- 25 <= Support < 35: `LowSupportDays`をfreeze。
- Support >= 35: `LowSupportDays=0`。
- `LowSupportDays >= 365`: Dissolution。

## Renewal

```text
if SettlementSupport == 100 and SupportPotential >= 80:
    SaturatedDays += 1
else:
    SaturatedDays = 0
```

365日連続で満たしたらRenewalを発生させる。

- SettlementはActiveのまま。
- Support=50、SaturatedDays=0、LowSupportDays=0へreset。
- Renewal Event / Pinを発行する。
- Renewal回数、前回日、間隔、直後人口・Pressureを統計へ残す。

Renewalは新Settlement生成や能力bonusではない。

## Fission eligibility

次をすべて満たす日だけ`FissionPressureDays++`し、それ以外は0へresetする。

- World PhaseがOrder。
- SettlementがActive。
- SettlementSupport >= 35。
- Active Invasionがない。
- Active Fission / Migrationがない。
- SettlementPressure >= 0.40。

90日連続したら、まずFission hotspotを探索する。閾値と期間はConfigである。

## Hotspot

親Settlement周辺の5×5領域を候補として、次をすべて満たす必要がある。

- candidate centerが既存Active Settlement Influenceの外側。
- 親CenterとのChebyshev距離が8～24。
- 直近30日の5×5内All Alive NPC Resident-Daysが90以上。
- 現在の5×5内Alive NPCが3人以上。
- Center cellがMap / occupancy規則上有効。
- child Coreが非親SettlementのCoreと重ならない。
- child Influenceが非親SettlementのInfluenceと重ならない。

直接の親だけは、child centerが親Influence外、両Core非重複、非親Influence非重複を満たす場合にInfluence overlapを許す。overlap cellのSettlement benefitはActive Affiliationで決める。Unaffiliatedは双方へのAffinityを得てもよい。

Resident-Daysはその日に当該5×5へいた全Alive NPCを所属に関係なく加算する。さらに各Cellについて直近30日の`ResidentDaysByCell`を保持または同値に集計する。Alive NPC 1人が1日そのCellにいれば+1とし、Affiliated / Unaffiliated、所属Settlementを区別しない。ただしmigrant候補は親Settlement所属者のままである。

複数hotspotはResident-Days最大、親への距離が近い方、named seedによる決定論的tie-breakの順で選ぶ。scan順、collection順へ依存しない。

### Center cell selection

Center候補CellはMap内、NPCが通常占有可能、Landmarkではない、親からのFission距離8～24、親子Core非重複、非親SettlementとのCore / Influence非重複、その他既存Fission条件を満たす必要がある。Settlement CenterはDesignationであり物理占有物ではないため、現在NPCがいるCellも候補にできる。

有効Cellを次の固定順で選ぶ。

1. `ResidentDaysByCell`最大。
2. 同値なら評価snapshot時点でAlive NPCが存在するCell。
3. 同値なら5×5の幾何学的中心へのChebyshev距離が短いCell。
4. なお同値ならnamed seedによる決定論的tie-break。

Fission判定開始時のimmutable snapshotを使用し、Map scan、array、dictionary、同tickのNPC移動や別Settlement生成で結果を変えない。同時Fission競合は既存Settlement arbitrationを使う。

選択hotspotにValid Center Cellがなければ不成立とし、同じMaintenanceに次順位の有効hotspotがあれば評価できる。重複制限を迂回して無効Cellを採用しない。

## Migrants and child settlement

```text
MigrantCount = DeterministicRound(LivingAffiliatedMembers * 0.40)
minimum = 4
```

候補はAlive、親へActive Affiliation、Active Invasion不参加、他Migration不参加の人物。Rest中かどうかは除外理由にしない。Combat / Action / Affinity等を全知的に優先せず、named seedで一様に選ぶ。

Tick末にchild SettlementをActiveで生成し、`ParentSettlementId`、Support=50、SaturatedDays=0、LowSupportDays=0を設定する。Order中なら既存Order benefitsを受ける。migrantは`FissionFounder`となりActive Affiliationをchildへ変更し、child Affinity +10と強いMigration Biasを持つ。親Affinityは履歴値として残せるが、Active membershipは一つだけである。

child Core内のUnaffiliatedはchild Affinity +7を得るが、強制加入しない。Migration Biasは通常Moveをchild方向へ強く歪め、Flee、Active Invasion、emergencyを上書きしない。

### Migration completion

次を満たした時点でMigration完了とする。

```text
Alive
AND ChildSettlement is Active
AND ChebyshevDistance(MigrantCurrentPosition, ChildSettlementCenter)
    <= ChildSettlementInfluenceRadius
```

現行Influence radiusは7。Core到達、Affinity閾値、Rest、Communicationを要求しない。Fission成立時点ですでにchild Influence内なら即時完了としてBiasを付けない、または直ちに解除し、完了Statisticsへ一度だけ計上する。

Move、Flee、その他Position変更ActionではAction全体の最終Position確定後に評価する。2マスMoveの1マス目では解除せず、方向選択を途中で変えない。Position変更がなくてもTick末Maintenanceでfallback評価し、二重Eventを発生させない。

完了時はMigrationBiasとActiveMigrationを解除し、MigrationCompleted Eventまたは増分Statisticsを更新し、通常のchild Home Biasへ移行できる。Active AffiliationはFission成立時にすでにchildへ変更済みなので、完了時に再変更しない。

死亡、child Dissolution、Integration等で元childが非Active、その他World Stateでtargetが無効になった場合はMigrationを中断する。Bias / ActiveMigrationを解除し、現在の有効AffiliationからHome Biasを再計算し、なければUnaffiliatedとする。死亡時は完了Eventを出さない。

Migration中にActive Invasionへ参加した場合はInvasion Biasを優先し、Migration Biasを保持しても通常Moveへ適用しない。Event終了後、未完了かつchildがActiveならMigrationを再開する。

親は成功時に`FissionPressureDays=0`、`HighPressureDays=0`とし、同tickにInvasionを開始しない。Supportを直接減らさない。人口流出の結果は翌日以降のP / R / S / Pressureに反映する。

## Parent / child peace

直接親子関係が有効な間だけ次を適用する。

- Invasion targetに選ばない。
- Affiliation差だけでExplicit Attack Candidateを作らない。
- 平時Collisionをcombatへ変換しない。
- Frictionだけで戦争へ進めない。

実際にAttackを受けた場合のCounterattack、Threat Memory、Fleeは維持する。この非侵略は兄弟、祖父母、孫へ自動継承しない。どちらかのDissolution / Integrationで無効になる。

## Fission before invasion

Pressure起因の処理順は固定する。

1. `FissionPressureDays`が90日未満ならInvasionを開始しない。
2. 90日以上で有効hotspotがあればFissionする。
3. 有効hotspotがない場合だけ、既存のcooldown、target、mobilization等を再ValidationしInvasionを開始できる。
4. 直接親子Settlementをtargetから除く。

Fission成功candidateがある日にInvasionへfallbackしてはならない。
