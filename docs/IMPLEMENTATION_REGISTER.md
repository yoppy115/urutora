# 正史外・実装管理台帳

**Status:** Non-canon implementation register

## Purpose

ユーザーから明示された実装要望や、実装に必要だがゲーム挙動を定めない技術的既定値のうち、既存の正史文書だけでは追跡しづらい要素を管理する。ここはゲーム仕様の正本ではない。ゲーム挙動へ関係する採用事項は対応する `docs/design/`、`docs/architecture/`、Accepted ADR、または `simulation/` に反映し、この台帳から参照する。

DraftやTBDをこの台帳だけで確定仕様に変えてはならない。

## Current register

| Item | Status | Canon / configuration route | Implementation note |
| --- | --- | --- | --- |
| App内の世界生成command | Implemented | `PLAYER_OBSERVATION.md` | buttonまたはCtrl+N。進行中は現在runを停止し、処理中の日が完了してから新規観測runを生成 |
| World runの連番 | Implemented | `observation-app.json` | release versionごとに `world-NNNN`。各versionで0001から開始し、同version内は既存最大番号+1 |
| World別ログ | Implemented | `LOGGING.md` | 観測中は`logs/vX.Y/world-NNNN/`。完了後は検証済みZIP＋SHA-256へ原子的に圧縮。生ログのためGit管理外 |
| NPC番号とクリック詳細 | Implemented | `PLAYER_OBSERVATION.md` | 既存の安定した数値Entity IDを使用。Coreのread-only projection |
| NPC行動履歴 | Implemented | `PLAYER_OBSERVATION.md` | NPCがactor / targetの既存EventからMove / MoveFailedだけを除外。新規Eventや因果を生成せず、表示上限500件をApp Config化 |
| 世界統計graph / table | Implemented | `PLAYER_OBSERVATION.md`, `V0_15_ECOLOGY.md` | 人口・平均年齢graph、選択Action、死因、現在年齢分布、繁殖・TargetAbsent・Combat・Held Information・Concept統計をread-only表示 |
| 現在年齢分布の表示bin | Implementation detail | `simulation/configs/observation-app.json` | 生存NPCだけを0歳から隙間なく0.5年幅で集計。0件binも表示し、人数・構成比・相対barを表示 |
| App観測設定 | Implemented | `simulation/configs/observation-app.json` | schema 3。既存項目に完了World圧縮と旧release log削除policyを追加 |
| Windows Formsの具体layout・色・描画 | Implementation detail | `ARCHITECTURE.md` | Simulation規則ではなく交換可能なPresentation詳細 |
| Update情報の取得順 | Workflow | `AGENTS.md` | Git commit / diff / tag / branchと正史を一次情報にし、過去chatを更新根拠にしない |
| 管理者権限が必要な導入 | Workflow | `AGENTS.md` | 回避試行を重ねず、対象とrollbackを限定した管理者用installerを別成果物として作る。自動実行しない |
| Codex独自変更の記録先 | Workflow | `AGENTS.md` | 正史外のtooling、workflow、技術既定値は本台帳へ記録し、ゲーム仕様と混同しない |
| GitHub Actions CI | Implemented | `.github/workflows/ci.yml` | push / pull request / 手動実行でWindows + .NET 10 build、全test、replay smokeを実行。repository内容のread権限だけを使用 |
| SimRunner / deterministic replay | Implemented | `ARCHITECTURE.md`, `TESTING.md` | `Simulation.Runner` CLIが完全Config、seed、tick数、code version、空の外部入力列、Event/最終状態SHA-256を保存し、再実行結果のdriftをexit code 2で検出。通常build対象かつpublish時に `tools/Simulation.Runner/` へ出力 |
| Replay state fingerprint | Implementation detail | `Simulation.Core` | Replay専用のread-only SHA-256。全NPC内部状態、情報、系譜、出生queue、Event等を安定順でhash化し、Decision入力やゲーム進行には使用しない |
| FsCheck | Implemented | `TESTING.md` | test-only dependency `FsCheck 3.3.4`。固定FsCheck replay seedで32 caseのrun seed / 日数 / 観測位置を生成し、Event列と最終stateの一致を検証 |
| NuGet audit到達不能時の扱い | Implementation detail | `Directory.Build.props`, `NuGet.Config` | 公式nuget.orgのみを使用。audit service自体へ到達できない `NU1900` だけを非fatalにし、実際の脆弱性警告 `NU1901`–`NU1904` はwarnings-as-errorsの対象に維持 |
| Git build provenance | Implemented | `ARCHITECTURE.md`, `ADR-0016` | commit/tree stateをassemblyとrun metadataへ埋め込み、clean treeだけをrelease publishの既定とする。成果物hashはrelease manifestへ保存 |
| Repository hygiene | Implemented | `tools/` | 生成物の誤追跡、不要な`.gitkeep`、Markdown local linkをCIとbaseline確定前に検査。finalizerはbranch/commit/tag/clean確認を行いpushしない |
| Git ACL用管理者finalizer | Implemented | `AGENTS.md`, `tools/` | 通常processが`.git`をOSに拒否された場合だけ使うWindows PowerShell 5.1対応wrapperとCMD入口。対象をurutoraのbranch/index/commit/tagへ限定し、OS設定変更やpushは行わない |

## Open non-canon implementation items

- World snapshotの保存・再開、削除、名前変更、比較画面は未実装であり、この台帳から仕様を推測しない。
- graphの長期downsampling、ログschema migrationは未決。現在のUI保持上限を超えた点は画面から落ちるが、CSVログには全日分を保持する。
