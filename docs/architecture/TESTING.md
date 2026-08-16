# v0 Headless Verification

**Status:** Baseline test obligations / Draft test implementation

`Simulation.Core.Tests` はGUIなしでCoreを高速実行する。失敗時にはCode Version、Config、RunSeed、tick、関係Entity ID、random purposeを再現可能な範囲で報告する。

## Required invariant groups

### Subjective decision boundary

- Decision層がReality型を直接読めない。
- 未観測Realityだけを変更しても、同じPerceptionとseedの候補順位・選択は変わらない。
- 同じPerceptionとseedから同じDecisionを得る。
- 同じNeed、Perception、seedから各Action Utilityを同値再現する。
- Attack UtilityがRealityのTarget Combat / HPを直接参照しない。
- PerceivedCombatを変えるとAttack / Flee Utilityが変化する。
- Pursuitの `U_attack` が通常Attackと同じ対象別定義を使う。

### Utility and scheduling

- 候補0件でIdle、1件で確定、2件で両方、3件以上でTop 3外を通常選択しない。
- 同点、負Utility、極端temperature等のedge caseが明示規則に従う。
- Action競合の結果がEntity配列順やDictionary列挙順に依存しない。
- 1日の最大Action数を超えない。

### Actions and reactions

- NPC占有CellへのMoveをCollision Attackへ変換し、同Actionで移動しない。
- CounterattackからCounterattackを再帰させない。
- Pursuit AttackからCounterattack、Flee、Pursuitを再帰させない。
- Communicationが送信者のHeld Information外から情報を作らない。
- 数値distortionが設定上限を超えず、SubjectSwap率と置換候補境界を守る。
- Observation誤差が距離ごとの最大値を超えない。
- Communication受信Confidenceがsource Confidenceを上回らない。
- EffectiveCommunicationが10を超えてもdistortion率とSubjectSwap率が負にならない。
- 失敗した通常能動ActionにもActivity -2 / Rest +0.5を適用する。
- Reactionには通常Action用Activity / Rest変化を適用しない。

### Reproduction and lifecycle

- 非遺伝情報、Exposure、ConceptMarkが子へ渡らない。
- MutationとBirthLocationがseedで再現可能。
- Birth位置競合と空きCellなしの失敗が規則通り解決される。
- ConceptMarkがBase遺伝値を書き換えない。
- Vitality curve schemaが確定Life Phase形状と滑らかなcontrol-point接続を表現できる。具体DailyVitalChange値と自然死亡時期は仕様確定まで固定しない。
- Reproduction Reject時にReproduction NeedとCooldownを変えない。
- CurrentHP 0以下のNPCが後続Micro RoundやReactionへ参加しない。
- 死亡Cellを後続Micro RoundとBirth解決で利用できる。
- 複数BirthRequest競合の結果がqueue順変更で変わらない。

### Whole-run determinism

- 同じCode Version、Config、RunSeedから同じSimulation Event列を得る。
- render頻度、frame rate、Event Log表示有無を変えてもEvent列が変わらない。
- 無関係なpurposeの乱数利用追加が、既存purposeのstreamをずらさない。

### v0.15 ecology update

- Targeted ActionをMove / Flee / Restより先に解決する。
- AttackされたNPCのIntent再評価は同一Micro Round最大1回で、複数Attackでも増えない。
- Reproduction Rejectは相手の既存Intentを維持し、Acceptは最大1回だけ置換する。
- TargetAbsent後、同じ古いPositionを使うTargeted Actionを反復しない。
- Reproduction Candidateが対象RealityのCooldown / HPを読まず、ResolutionがHP / Cooldown / Distanceを検証する。
- Held InformationはSubject + Propertyごとに3件を超えない。ただしEviction結果は仕様確定まで固定しない。
- MatureAge 180日、ReproductionCooldown 90日、ThreatMemoryDuration 90日、ReproductionNeedGain +0.04/dayをConfig defaultとして検証する。
- BaseMaxHP約50 scaleと新Damage係数 `4 + 0.9*AttackCombat - 0.4*DefenseCombat`、Random(0.9,1.1)を検証する。
- Hit Rate、Counterattack構造、Concept Exposure / Mark値がv0.15で変化していないことを回帰検証する。
- InitialAgeが180〜700日のConfig範囲からseed付き生成される。

具体的なtest framework、fixture形式、統計的試験のsample数は実装時に決める。
