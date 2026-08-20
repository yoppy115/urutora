# Project Instructions

このファイルはCodexの作業規則だけを定める。ゲームの現行仕様は `docs/`、実験値は `simulation/` を正史とし、チャットログや口頭の案を自動的に仕様扱いしない。

## Before changing anything

1. 最新の `main`、`README.md`、`docs/INDEX.md` を確認する。
2. 変更対象に対応する `docs/design/` の文書を読む。
3. `docs/architecture/` と関連ADRを確認する。
4. `Draft` や `TBD` を推測で埋めない。不足する判断を明示する。
5. 文書間に矛盾があれば、実装で片方を黙って選ばず報告する。

## Execution behavior

- 変更・実装を依頼された場合、必要な調査、影響確認、設計検討はタスク内部で行う。
- ユーザーが明示的に計画やレビューだけを求めていない限り、事前の実装案や計画を別ターンで提示せず、そのまま変更と検証まで進める。
- Baselineとの矛盾や、未確定のDraftを決めなければ進められない箇所は勝手に仕様化しない。その箇所だけ保留し、安全に進められる残りの作業は継続する。
- 結果を大きく変える未決事項、破壊的操作、または新しい外部権限が必要な場合だけ、ユーザーへ判断を求める。
- 作業後は、変更内容、変更ファイル、検証結果、保留した判断だけを簡潔に報告する。

## Design principles

- システムをモジュール化し、疎結合に保つ。
- 巨大な `GameManager` や全状態を握る万能クラスを作らない。
- Reality（客観状態）とNPC Perception（主観認識）を別のデータ層にする。
- NPCの判断は主観情報だけを使い、非公開のRealityデータを直接読まない。
- NPCは主観上可能だと思う行動を選び、Reality側の解決で失敗し得る。
- v0 Action Utilityは `docs/design/UTILITY_AI.md` の式を正本とし、未記載の情報価値・関係・先読みを独自追加しない。
- Utility AIは候補が3件以上なら上位3候補へ絞り、Utility由来の重みで確率選択する。候補2件は両方を対象にする。
- 確率処理にはrun seedから用途別に派生する乱数streamを使い、再現に必要なseedとConfigを保存する。
- 内部数値を自動的にプレイヤーUIへ露出しない。
- シミュレーションは表示層やゲームエンジンなしでも実行・テスト可能にする。
- interface、event、data-driven configuration、交換可能なmoduleを優先する。
- 無関係なシミュレーションシステム間の直接依存を避ける。
- LLMをSimulation Coreの権威的な計算へ使用しない。
- 遺伝にはBase能力、Simulationの実能力にはEffective能力を使い、ConceptMarkでBase値を変更しない。
- HP 0以下のNPCは即時に行動不能・非占有とし、tick末cleanupまで行動させない。
- Move/Birth等の競合結果を配列、queue、生成、collection列挙順で決めない。
- v0.15のTargeted ActionはAttack → Reproduction → Communication順で、終了済みphaseへ再抽選Intentを巻き戻さない。
- Vitality Control Point値は確定Phase形状を守る保守的なv0.15 Config初期値として調整できる。
- v0.2.5の知識はPersonBelief / EventBelief / SettlementBeliefへ分離する。旧Subject + Property 3件FIFOを復活させず、Unknown、field provenance、人物capacity / TTL、Communication差分送信を `docs/design/KNOWLEDGE_MEMORY.md` に従って扱う。
- EventBeliefはMandatory Memorable Eventまたは認識済みPin Importance 60以上だけを候補とし、Reality Eventを全NPCへ自動配布しない。
- Person evictionはAggregatePersonConfidenceとPosition Unknown = PositiveInfinityを使う。SettlementBeliefは自己所属、直接観測、当事者Event、Communication、直接Outcomeだけから取得する。
- v0.2 Settlement / Order仕様は `docs/design/V0_2_SETTLEMENT_ORDER.md` を正本とし、Generation中の形成・AffinityとOrderから有効な社会Ruleを混同しない。
- v0.2.1はHotspotを90日、5×5、成功3件、15日評価とする。v0.2.3は既存Influence内SuccessをHotspotから除外し、新Core全Cellを既存Active Influenceと非重複にする。
- v0.2.3出生所属は、同じActive Settlement所属の両親なら場所非依存、片親所属なら受胎時に両親とも所属先Influence内、異所属なら両親が同じ一意なActive Core内という境界を守る。
- v0.2.4の履歴は `docs/design/V0_2_4_SETTLEMENT_STABILIZATION.md`、現行overrideは `docs/design/V0_2_5_KNOWLEDGE_FISSION_INVASION.md` とその正本群を使う。
- 自然消滅は累積SettlementSupport、更新はRenewal、高PressureはFissionを先に評価する。旧CoreOccupancy / BlockedMovement Crowding、raw Friction加算、Pressureからの直接Invasionを復活させない。
- CenterにはVictory ruleを持たせず、Attack VictoryはUsable Core 50%以上を3日連続とする。Defense Victoryとparticipant stateは `INVASION_V025.md` に従い、征服所属変更はAlive NPCだけに行う。
- Invasion cohortをCombat / Action値で全知的に選ばない。通常RestはFieldRest、HP比20%以下のRest / FleeだけがRetreatingとなり、同じEventへ戻らない。
- Recent Event Buffer、Incremental Statistics、Historical Milestone、Optional Raw Archiveを分け、表示・保持・archive設定でSimulation結果を変えない。
- Order中のCollision、Friction、Invasion、Auraは所属・WorldPhase・Event状態を明示的に解決し、v0.15の主観境界やTargeted Action順を弱体化しない。
- v0.2 Settlement構造変更は固定順のTick末Maintenanceでcommitし、新規Settlement / WorldPhase / Invasion開始は原則翌Tickから反映する。
- Hotspot arbitration、Friction、SettlementPressure、Mobilization、Unaffiliated保護、同一Core繁殖、Aura / temporary MaxHPは `V0_2_SETTLEMENT_ORDER.md`、Support / Fissionは `SETTLEMENT_FISSION.md`、Invasion離脱・勝敗は `INVASION_V025.md` の確定境界を守る。
- Fission CenterはCell別Unaffiliated Resident-Daysを優先し、Migration完了はchild Influenceへの実到達で判定する。Struggleはv0.2.5のWorldPhaseではなくBacklogである。

## Change contract

- 原則として安定版の `main` に直接機能実装せず、作業ブランチで差分を作る。
- 現行仕様を変える場合は、同じ変更で対応する設計文書を更新する。
- アーキテクチャ上重要、または後戻りしづらい判断にはADRを追加する。
- シミュレーション変更には、実用的な範囲で決定論的テストを追加する。
- ランダム挙動のテストではseedを固定し、失敗時にseedを表示する。
- 調整値とゲームデータはコードへ埋め込まず、設定またはデータファイルへ分離する。
- 新しいゲームエンジン、言語ランタイム、外部依存は、採用理由を示して合意後に追加する。
- 生ログをGitへ追加しない。保存価値のある実験だけを `research/` に要約して残す。

## Documentation and history

- 文書は日本語を基本とし、コード識別子とファイル形式上のキーは英語を基本とする。
- 文書内でBaselineとDraftが混在する場合、節ごとに状態を明記する。
- ADRは `docs/decisions/ADR-NNNN-short-title.md` 形式で連番にする。
- コミット接頭辞は内容に応じて `feat:`, `sim:`, `balance:`, `design:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:` を使う。
- Gitログは開発変更の履歴、`logs/` は世界内イベントの実行ログとして混同しない。
- Run比較では `Version`、`repositoryCommit`、Config、Seedを一組とし、同じVersion名でも異なるcommit世代を混在させない。

## Completion checklist

- 関連文書とADRに矛盾がない。
- テストまたは妥当な検証を実行し、結果を報告した。
- 設定・seed・再現手順が、変更の性質に応じて保存されている。
- 未決事項を実装上の既成事実にしていない。
