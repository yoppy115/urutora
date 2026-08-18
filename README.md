# World Sim（仮称）

単純な個体・概念・困難から始まり、主観を持つ住民たちの適応、誤解、歴史が、次世界の上位存在・詩篇・新たな困難へ変換されることで、世界そのものが世代を超えて進化していく観測型シミュレーションです。

プレイヤーは世界を完全支配せず、限られた啓示を投げ込み、世界から返ってくる予想外の答えを観測します。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**です。

## Current status

- フェーズ: v0.2.6 Fission / Invasion Throughput Update
- 実装: headless Core、Windows Desktop観測App、SimRunner、全自動testへv0.2.6を反映済み
- 実装基盤: C# / .NET 10。`Simulation.Core`、`Simulation.App`、`Simulation.Runner`、各Testsを分離
- 外部依存: test-onlyのFsCheck 3.3.4。Production Core / App / Runnerは外部packageなし

v0.2.5までの境界を維持しながら、v0.2.6の全NPC Fission hotspot、Invasion全所属者参加、Center距離連動の攻撃者不在判定、開始間隔60日を同一seedで再現可能なCoreへ反映する。Windows Formsは交換可能なPresentation Adapterであり、GUIはRealityの権威を持たない。

## Source of truth

| 情報 | 正式な置き場所 |
| --- | --- |
| 現在のゲーム仕様 | [`docs/design/`](docs/design/) |
| モジュール境界と依存方向 | [`docs/architecture/`](docs/architecture/) |
| 重要な判断と採用理由 | [`docs/decisions/`](docs/decisions/) |
| 未採用・保留・却下案 | [`docs/ideas/BACKLOG.md`](docs/ideas/BACKLOG.md) |
| 調整値・プリセット・ゲームデータ | [`simulation/`](simulation/) |
| 保存対象の実験結果 | `research/`（設定・要約・所見を保存） |
| 実行時の生ログ | `logs/`（Git管理外） |

チャットは議論の場であり、その全文は仕様ではありません。採用した結論だけを関連文書と、必要ならADRへ反映してから実装します。文書間の矛盾は、推測で片方を選ぶのではなく修正すべき不整合です。

文書の入口は [`docs/INDEX.md`](docs/INDEX.md)、版系譜は [`docs/VERSION_LINEAGE.md`](docs/VERSION_LINEAGE.md) です。

## Development flow

1. `main` を現在の安定した正史として扱う。
2. 変更ごとに作業ブランチを作る。
3. 関連設計書・アーキテクチャ・ADRを読んでから変更する。
4. 必要な設計検討はタスク内部で行い、変更、検証、簡潔な報告まで一続きで進める。
5. 実装、テスト、文書更新を同じ差分へ含める。
6. Pull Requestまたはローカル差分でレビューしてから `main` へ統合する。

未確定の判断が必要な箇所だけを保留し、独立して進められる作業は継続します。

## Reproducible experiments

- すべての確率的な実行で乱数seedを保存する。
- 調整値はコードから `simulation/configs/` へ分離する。
- 通常の巨大ログは `logs/` に出力し、Gitへコミットしない。
- 完了Worldは同release directory内の検証済みZIPとSHA-256 sidecarへ圧縮する。
- 残す価値のある実験だけ、設定・要約・注目イベント・所見を `research/` に保存する。
- Run比較ではVersionだけでなく`repositoryCommit`、Config、Seedを併記する。

## Build and run

.NET 10 SDKとWindowsが必要。初回test build時はFsCheck取得のため公式NuGetへの接続が必要。

```powershell
.\build.ps1 -RunTests
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build
```

配布用EXEはclean Git commitから次で生成する。

```powershell
.\publish.ps1
```

成果物は `outputs\World Sim v0.2.6\Simulation.App.exe` と `outputs\World Sim v0.2.6\tools\Simulation.Runner\Simulation.Runner.exe`。同梱Configを使い、ログはreleaseごとに `logs/v0.2.6/world-NNNN/`、完了後はZIPへ保存する。

headless実行と決定論的replay:

```powershell
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build -- --headless --ticks 120 --seed 8147291
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- record --config simulation\configs\v0-default.json --seed 8147291 --ticks 120 --output work\replays\v0.2.6-seed-8147291.json
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- verify --replay work\replays\v0.2.6-seed-8147291.json
```

Appは世界生成、明示的な世界完了、指定年数×World回数の自動実行、1/2/3/5/10/50倍の速度制御、Settlement / Core / Influence / Invasion Map、ConceptMark旗、NPCとSettlementの現在状態、人口・所属率・平均年齢graph、累積Action選択を提供する。重い系譜・行動履歴・死因・年齢分布・社会・戦闘・繁殖・Friction・Support・Rest診断はDesktop描画から外し、headless統計とWorldログに保持する。

## Next validation

固定seedでv0.2.5とv0.2.6を比較し、Fission成立数、Invasion動員・期間・間隔、人口、Settlement寿命の因果を観測する。Struggle、汎用Pin Importance式、Event / Settlement Belief容量は先取りしない。
