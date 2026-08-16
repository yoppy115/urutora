# v0 Headless Verification

**Status:** Baseline test obligations / Implemented test tooling

`Simulation.Core.Tests` はGUIなしでCoreを高速実行する。失敗時にはCode Version、Config、RunSeed、tick、関係Entity ID、random purposeを再現可能な範囲で報告する。

## Implemented tooling

- `Simulation.Core.Tests`: 例示・境界・回帰testに加え、test-onlyのFsCheck 3.3.4を使用する。
- FsCheckは固定replay seedから32 caseのRunSeed、実行日数、途中観測位置を生成し、Event列と最終state fingerprintが一致することを検証する。失敗時はFsCheckの縮小counterexampleとreplay seedを出力する。
- `Simulation.Runner.Tests`: replay記録と再検証、Event hash drift、埋め込みConfig改変拒否を検証する。
- GitHub ActionsはWindows + .NET 10で全testを実行し、その後SimRunner CLIで10 tickのrecord / verify smokeを行う。

## Deterministic replay commands

`Simulation.Runner` は完全Config、RunSeed、tick数、code version、空の外部入力列、期待Event hash、期待最終state hashをJSON envelopeへ保存する。`verify` は埋め込みConfigだけで新規Worldを再実行し、主要集計値とSHA-256を比較する。hash drift時は終了code 2、入力・schema不正時は終了code 1を返す。

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
- Vitality curve schemaが確定Life Phase形状と滑らかなcontrol-point接続を表現できる。Config初期値は、Phaseごとの符号・強弱、曲線の連続性、BaseMaxHP約50、1.5歳以降は自然回復なし、3歳前後からの強減衰、および不連続な大量死を起こさないという制約を満たす。
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
- Held InformationはSubject + Propertyごとに3件を超えず、4件目で最古をFIFO削除する。
- MatureAge 180日、ReproductionCooldown 90日、ThreatMemoryDuration 90日、ReproductionNeedGain +0.04/dayをConfig defaultとして検証する。
- BaseMaxHP約50 scaleと新Damage係数 `4 + 0.9*AttackCombat - 0.4*DefenseCombat`、Random(0.9,1.1)を検証する。
- Hit Rate、Counterattack構造、Concept Exposure / Mark値がv0.15で変化していないことを回帰検証する。
- InitialAgeが180〜700日のConfig範囲からseed付き生成される。

### v0.15 resolved patch

- 4件目のHeld Information取得で最古記録を削除し、低Confidenceの新情報でもFIFO順を変えない。
- Subject死亡を直接確認すると全Propertyを削除する。
- TargetAbsentではPositionだけを無効化し、Subject全体を削除しない。
- Communicationによる死亡伝聞だけではSubject全体を削除しない。
- 同一Micro RoundのTargeted ActionをAttack → Reproduction → Communication順で解決する。
- Attackで死亡したTargetへの後続Reproduction / Communicationを成立させない。
- Attack後にHP条件が崩れたReproductionをReality Validationで失敗させる。
- Interrupt再抽選Attackを終了済みAttack Phaseへ巻き戻して実行しない。
- Vitality Configが0〜0.5歳の回復力上昇、0.5〜1歳の強回復、1〜1.5歳の回復低下、1.5〜2.5歳の弱減衰、2.5〜3歳の減衰加速、3歳以降の強減衰を満たす。

追加の統計的試験sample数と、意図的なSimulation変更時に保存するgolden replayの運用は未決である。
