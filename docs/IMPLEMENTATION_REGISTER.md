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
| NPC番号とクリック詳細 | Implemented | `PLAYER_OBSERVATION.md` | 既存の安定した数値Entity IDを使用。Coreのread-only projection。Combat由来Death EventでNPC別キル数を表示 |
| NPC行動履歴 | Implemented | `PLAYER_OBSERVATION.md` | NPCがactor / targetの既存EventからMove / MoveFailedだけを除外。新規Eventや因果を生成せず、表示上限500件をApp Config化 |
| 世界統計graph / table | Implemented | `PLAYER_OBSERVATION.md`, `V0_15_ECOLOGY.md`, `STATISTICS.md` | 人口・所属率・平均年齢graph、選択Action、死因、現在年齢分布、World Phase、非消滅Settlement、Invasion、Aura、繁殖・Combat等をread-only表示。FrictionはSettlement詳細へ分離 |
| 現在年齢分布の表示bin | Implementation detail | `simulation/configs/observation-app.json` | 生存NPCだけを0歳から隙間なく0.5年幅で集計。0件binも表示し、人数・構成比・相対barを表示 |
| App観測設定 | Implemented | `simulation/configs/observation-app.json` | schema 6。完了World圧縮、旧release log削除、Worldログflush間隔、全履歴diagnostics間隔、automatic advanceのwork slice / cooldownを管理 |
| World完了command | Implemented | `LOGGING.md`, `observation-app.json` | `世界完了`で現在runのstreamを閉じ、`completion.json`を最後に確定して検証済みZIPへ圧縮。通常のApp終了・強制終了は未完了のまま保持し、次回起動時に誤圧縮しない |
| 指定年数反復実行 | Implemented | `observation-app.json` | Appで整数年数とWorld回数を指定。各Worldを`years * daysPerYear` tickで完了・圧縮し、release内の次番号・次seedで反復。最後のWorldは完了状態のread-only表示で停止。Core規則とtick順は変更しない |
| 旧BIOS向け負荷抑制 | Conservative technical default | `v0-default.json`, `observation-app.json` | CPU並列度defaultを全24論理CPU自動から8へ制限し、自動進行を2日slice + 15ms cooldownで実行。Simulation結果は直列・並列同値を維持し、wall-clock負荷だけを抑える。firmware起因BSODの絶対保証ではない |
| Windows Formsの具体layout・色・描画 | Implementation detail | `ARCHITECTURE.md` | Simulation規則ではなく交換可能なPresentation詳細 |
| Update情報の取得順 | Workflow | `AGENTS.md` | Git commit / diff / tag / branchと正史を一次情報にし、過去chatを更新根拠にしない |
| 管理者権限が必要な導入 | Workflow | `AGENTS.md` | 回避試行を重ねず、対象とrollbackを限定した管理者用installerを別成果物として作る。自動実行しない |
| Codex独自変更の記録先 | Workflow | `AGENTS.md` | 正史外のtooling、workflow、技術既定値は本台帳へ記録し、ゲーム仕様と混同しない |
| GitHub Actions CI | Implemented | `.github/workflows/ci.yml` | push / pull request / 手動実行でWindows + .NET 10 build、全test、replay smokeを実行。repository内容のread権限だけを使用 |
| SimRunner / deterministic replay | Implemented | `ARCHITECTURE.md`, `TESTING.md` | `Simulation.Runner` CLIが完全Config、seed、tick数、code version、空の外部入力列、Event/最終状態SHA-256を保存し、再実行結果のdriftをexit code 2で検出。通常build対象かつpublish時に `tools/Simulation.Runner/` へ出力 |
| Replay state fingerprint | Implementation detail | `Simulation.Core` | Replay専用のread-only SHA-256。全NPC内部状態、社会state、情報、系譜、出生queue、Event等を安定順でhash化し、Decision入力やゲーム進行には使用しない |
| FsCheck | Implemented | `TESTING.md` | test-only dependency `FsCheck 3.3.4`。固定FsCheck replay seedで32 caseのrun seed / 日数 / 観測位置を生成し、Event列と最終stateの一致を検証 |
| NuGet audit到達不能時の扱い | Implementation detail | `Directory.Build.props`, `NuGet.Config` | 公式nuget.orgのみを使用。audit service自体へ到達できない `NU1900` だけを非fatalにし、実際の脆弱性警告 `NU1901`–`NU1904` はwarnings-as-errorsの対象に維持 |
| Git build provenance | Implemented | `ARCHITECTURE.md`, `ADR-0019` | commit/tree stateをassemblyとrun metadataへ埋め込み、clean treeだけをrelease publishの既定とする。成果物hashはrelease manifestへ保存 |
| Repository hygiene | Implemented | `tools/` | 生成物の誤追跡、不要な`.gitkeep`、Markdown local linkをCIとbaseline確定前に検査。finalizerはbranch/commit/tag/clean確認を行いpushしない |
| Git ACL用管理者finalizer | Implemented | `AGENTS.md`, `tools/` | 通常processが`.git`をOSに拒否された場合だけ使うWindows PowerShell 5.1対応wrapperとCMD入口。対象をurutoraのbranch/index/commit/tagへ限定し、OS設定変更やpushは行わない。同一version追補は`-NoTag`で既存tagを移動しない |
| v0.2.4 Config schema | Implemented | `V0_2_4_SETTLEMENT_STABILIZATION.md`, `simulation/configs/v0-default.json` | schema 3 / `v0.2.4-default-2`。Action別疲労、Move Bias、Proto-Order、Support、Invasion guardrailと同版追補Advance defaultを型付き設定へ分離 |
| Settlement domain分割 | Implemented | `MODULES.md`, `ADR-0016`–`ADR-0018` | `SettlementQueries`、Formation、Maintenance、Invasion、ConceptAuraをCore内の別責務にし、Appはprojectionだけを読む |
| Advance / Defense / Cohesion weight | Configurable detail | `V0_2_SETTLEMENT_ORDER.md`, `v0-default.json` | `ln(5)` / 2.0 / 0.75。Advanceは距離差に指数適用し、敵Coreへ1 Cell接近×5 / 不変×1 / 離脱×0.2。Active参加者のHome / Foreignを無効化し、AdvanceがCohesionより常に強いvalidationを持つ |
| Aura更新境界 | Implementation detail | `ADR-0018`, `SIMULATION_TICK.md` | Tick開始・各Micro Round前後・Concept Exposure後に決定論的snapshotを再計算。一時MaxHP解除ClampはDamage portを通さない |
| v0.2.4 log schema | Implementation detail | `LOGGING.md`, `STATISTICS.md` | `run.json` schema 5を維持し、Event wrapperを4、diagnosticsを5へ更新。Action別疲労、Bias、Support、re-armを追加し、release別World連番・圧縮・SHA-256を維持 |
| v0.2.1 Hotspot補正根拠 | Configurable detail | `V0_2_SETTLEMENT_ORDER.md`, `v0-default.json` | clean v0.2 world-0001（seed 8147291、385日、Success 152）とworld-0002（seed 8147292、290日、Success 132）を分析。旧4×4評価日の最大集中数が両方3、Candidate / Rejection / Formationが0だった。現行defaultはthreshold 3、5×5。90日、15日評価、spacing 7は維持 |
| Patch release directory | Implementation detail | `ReleaseIdentity.cs`, `publish.ps1` | patch番号が1以上ならApp表示、World log、publish先を `vMajor.Minor.Patch` とする。patch 0の既存releaseは従来どおり `vMajor.Minor` |
| Event時点の所属projection | Implementation detail | `STATISTICS.md` | Event payloadとreplay fingerprintへActor / TargetのEvent発生時Settlement IDを保持。所属別のAction・繁殖・Concept統計が後日の所属変更や解散で遡及変化しないようにする |
| Settlement Map描画 | Implementation detail | `PLAYER_OBSERVATION.md` | 60色のPresentation paletteでInfluence / Core / Center、所属outline、Active Invasion arrowを描画。Active中は色を固定し、消滅時にslotを解放して新規Settlementの決定論的抽選へ戻す。描画はCore state・乱数を変更しない |
| Settlement出生所属 | Implemented | `REPRODUCTION.md`, `V0_2_SETTLEMENT_ORDER.md`, `ADR-0016` | 両親が同じActive Settlement所属なら場所非依存で通常の親近傍出生、片親所属なら両親が所属先Influence内にいる場合に同Influence出生、異所属なら一意なCore条件で同Core出生。BirthRequestへ配置範囲を固定し、MembershipThresholdから開始。親Affinity値は複製しない |
| ConceptMark旗 | Implementation detail | `PLAYER_OBSERVATION.md` | NPC本体は中立色、Settlementは輪郭色、ConceptMarkはLandmarkと同色の小旗を複数描画。Presentation専用でCoreへ影響しない |
| v0.2.2速度段階 | Implementation detail | `PLAYER_OBSERVATION.md` | 100ms描画timerごとのSimulation batchを1 / 2 / 3 / 5 / 10 / 50日に固定。authoritative tickはUI thread外の単一順序workerで実行し、renderはbatch後だけ更新 |
| v0.2.2観測性能補正 | Implementation detail | `ARCHITECTURE.md`, `observation-app.json` | v0.2.2時点ではConcept AuraをSettlement / Concept / Position索引、Statistics Eventをtype索引、同一Tickの統計をcache化し、Coreは直列のままとした。ログはdefault 10日ごとにflushし、終了時は必ずflush |
| v0.2.2性能比較 | Verification record | `Simulation.App`, v0.2.1 release artifact | seed 8147292、120日、Worldログ有効の同一PC比較でv0.2.1 15.828秒、v0.2.2 14.493秒。比較用一時harnessはGit管理外 |
| v0.2.3決定論的CPU並列化 | Implementation detail | `ARCHITECTURE.md`, `ADR-0020`, `v0-default.json` | ObservationをObserver単位、初回Intent planをNPC単位で分離し、NPC ID順にmerge。Action Resolution / Event / Maintenanceは直列。並列度0は論理CPU自動、1は直列、現行安全defaultは8、default開始人口128 |
| v0.2.3知覚空間索引 | Implementation detail | `ARCHITECTURE.md`, `ADR-0020` | 各Observerの全Alive走査をやめ、Position索引からradius 3内だけをSubject ID順に取得。乱数key、Held Information順、Eventを維持 |
| Settlement詳細Tab | Implementation detail | `PLAYER_OBSERVATION.md`, `STATISTICS.md` | Active Center clickをNPC clickより優先し、形成・人口・Core / Crowdingと当該Settlement PairのFrictionをread-only表示。World社会表から消滅済みSettlementとFrictionを非表示化 |
| v0.2.3人口300性能比較 | Verification record | `Simulation.Core`, `v0-default.json` | seed 8147291、初期人口300、120日、Worldログ有効、24 logical CPUの同一PCでv0.2.2 default 27.985秒、v0.2.3直列27.379秒、v0.2.3自動並列15.057秒。Event/stateは直列・並列一致test済み。一時harnessはGit管理外 |
| v0.2.4 Move地域判定 | Conservative implementation detail | `V0_2_4_SETTLEMENT_STABILIZATION.md`, `Settlement Movement Policy` | 自領域Move疲労軽減は移動元と最終Cellがともに自Core / Influence内の場合に適用。Influence重複時のForeign weightは、回避側の最小倍率を退出側より優先し、複数Settlementによる倍率の無制限乗算を避ける |
| v0.2.4 Support初期window | Conservative implementation detail | `V0_2_4_SETTLEMENT_STABILIZATION.md`, `Settlement Support` | 成立後90日未満は存在する日数だけでAverage / totalsを算出する。分母のFormation thresholdとMemberDaysは正史式を維持し、365日hysteresisを早送りしない |
| v0.2.4観測projection | Implemented | `STATISTICS.md`, `LOGGING.md` | Settlement詳細へCore / Influence / 外部人数、Support P/R/S、LowSupportDays、Home / Foreign移動、armed / re-armを追加。世界統計へRest率、選択時Need / Pressure、Action別疲労、Invasion連続開始防止を追加 |
| v0.2.4同版Hotspot追補 | Implemented | `V0_2_SETTLEMENT_ORDER.md` | Reproduction Success時点の両親のActive所属IDを固定し、一方でも所属なら場所にかかわらず新規Formation集計から除外。既存Settlement Support集計は変更しない |
| v0.2.4長期実行最適化 | Conservative implementation detail | `ENGINEERING_REPRODUCIBILITY.md`, `LOGGING.md`, `observation-app.json` | Held InformationをFIFO・代表値・近傍・一様抽選索引化し、知覚view cache、移動occupancy、Threat対象だけの保護判定を採用。日次CSV / graphは軽量な正確集計、全履歴diagnosticsはdefault 30日間隔＋完了時。Core / Event順は直列・並列一致testで固定 |
| v0.2.4長期実行性能確認 | Verification record | `Simulation.Core`, `Simulation.App` | seed 8147291のheadless実測で現行default-2は365日10.865秒、1460日80.611秒で完走。変更前default-1は365日22.066秒、1460日は90秒の計測枠内に完了しなかった。ConfigとCommunication抽選実装が変わるため同一世界軌跡の厳密比較ではなく、wall-clock回帰の参考値 |

## Open non-canon implementation items

- World snapshotの保存・再開、削除、名前変更、比較画面は未実装であり、この台帳から仕様を推測しない。
- graphの長期downsampling、ログschema migrationは未決。現在のUI保持上限を超えた点は画面から落ちるが、CSVログには全日分を保持する。
