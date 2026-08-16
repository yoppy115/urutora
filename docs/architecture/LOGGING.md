# Logging

**Status:** Baseline constraints / Draft mechanics

## Baseline purpose

世界の再現、デバッグ、ピン抽出、歴史・詩篇生成のため、機械可読なログを保持可能な構造にする。

最低限、次の論理カテゴリを扱う。

- Reality Log
- Perception Log
- Decision Log
- Action Log
- Birth / Death Log
- Revelation Log
- Pin Log
- Inheritance Log

一つのイベントが複数カテゴリから参照されてもよい。ログカテゴリを必ず別ファイルへ分割する必要はない。

v0では少なくとも次の型を区別できる構造化Simulation Eventを持つ。

- Birth、Death
- Move、MoveFailed
- Communication
- Attack、CollisionAttack、Counterattack
- Flee、Pursuit
- ReproductionAttempt、ReproductionSuccess、ReproductionFailure
- ConceptMarkAcquired

Decision DebugとWorld Eventは概念的に分離する。Desktop AppのEvent Logは同じWorld Event streamのread-only projectionを使う。

## Baseline boundaries

- ログは世界の結果を変更しない。
- 生ログすべてをGitへ保存しない。
- 保存価値のあるrunだけ、設定、seed、commit、要約、注目イベントを `research/` に残す。
- ピンと詩篇入力は、元ログの安定IDを参照できるようにする。
- Perception LogはReality Logの単なる複製にしない。
- Event生成や表示頻度がSimulationの乱数消費・結果・処理順を変えない。
- Desktop観測中のWorldログは `logs/vX.Y/world-NNNN/` に分離し、World番号はrelease versionごとに0001から開始する。run metadataと各JSONL entryはrelease versionを保持する。
- `run.json`、`yearly_stats.csv`、`final_map.txt` 等のファイル出力はv0初期必須要件ではない。将来追加できる構造だけを保つ。
- Action Eventは通常ActionかReactionか、Attempt / Success / Failure、target、Utility内訳参照、ActionOutcomeを区別できる。
- DeathはHP 0以下になったResolution時点を発生tickとして記録し、tick末cleanup時点と混同しない。
- BirthRequest、希望Cell、競合tie-break、再抽選、BirthFailureを同一のstable request IDで追跡できる。
- TargetAbsent、Position invalidation、Interrupt理由、Intent replacement、再評価回数を追跡可能にする。
- Reproduction FailureはReject、Reality precondition failure、TargetAbsent等を機械可読に区別する。ただしNPC向けOutcomeへ非公開precondition値を露出しない。
- Held InformationのFIFO evictionと、Subject消滅直接確認による全Property purgeを理由付きで診断可能にする。World Event / History Log削除とは分離する。
- Targeted Action EventはAttack、Reproduction、Communicationのphase ordinalと、先行phase後の再Validation結果を保持できる。
- Interrupt再抽選Intentが実行、未処理phase待ち、終了済みphaseのため失効のどれになったかを追跡できる。

## v0.15 required run metrics

v0.15 Runでは少なくとも次を集計可能にする。

- Population: 日/年ごとの人口、最低人口、最終人口。
- Death: Combat、Vitality/Aging、その他、平均死亡年齢。
- Reproduction: Attempt、Success、Reject、Reality precondition failure、Failure理由別件数。
- Targeted Action: Attack / Communication / ReproductionのattemptとTargetAbsent。
- Combat: Collision、Explicit Attack、Counterattack、Pursuit Attack、平均Damage。
- Perception: TargetAbsentによるPosition invalidation、Held Information総数、NPCあたり平均/最大。
- Concept: Exposure、ConceptMark取得数。

目的は人口維持値の調整だけでなく、Population、Combat、Reproductionを生む因果を比較可能にすることである。

## v0.15 storage and provenance

- App Configの`archiveCompletedWorldLogs`がtrueの場合、完了Worldのdirectoryを同じversion directory内の`world-NNNN.zip`へ原子的に圧縮する。ZIP作成とentry検証が完了するまで元directoryを削除しない。
- ZIPごとに`world-NNNN.zip.sha256`を保存する。World連番は未圧縮directoryとZIPの両方を数え、archive後も番号を再利用しない。
- App Configの`deleteOtherReleaseVersionLogs`がtrueの場合、起動時のlog maintenanceで現行release以外の`vX.Y`形式directoryを削除する。対象はlog root直下へ限定し、任意pathを再帰削除しない。
- `run.json` schema 4は完全Configに加え、repository commit、tree state、Simulation Config SHA-256、Observation App Config SHA-256を保持する。
- release用publishはclean Git worktreeを必須とする。診断用dirty buildは明示的opt-inだけ許し、run metadataとrelease manifestへ`dirty`を残す。
- publish成果物はrepository commit、tree state、成果物別SHA-256を`release-manifest.json`へ保存する。

これらは保存・検証境界であり、Simulation Event、乱数stream、世界結果を変更しない。採用理由は[`ADR-0016`](../decisions/ADR-0016-log-retention-and-build-provenance.md)を参照する。

## Draft mechanics

- JSONL、binary、database等の保存形式。
- schema versionとmigration。
- snapshotとevent logの分担。
- 保存期間、sampling、個体数増加時の性能。
- Perceptionや関係情報の記録粒度。
- 詩篇生成へ渡す情報の選別と秘匿境界。
