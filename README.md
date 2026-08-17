# World Sim（仮称）

単純な個体・概念・困難から始まり、主観を持つ住民たちの適応、誤解、歴史が、次世界の上位存在・詩篇・新たな困難へ変換されることで、世界そのものが世代を超えて進化していく観測型シミュレーションです。

プレイヤーは世界を完全支配せず、限られた啓示を投げ込み、世界から返ってくる予想外の答えを観測します。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**です。

## Current status

- フェーズ: v0.2.3 Settlement境界・詳細観測・決定論的並列化
- 実装: headless Core、Windows Desktop観測App、SimRunner、全自動testを実装済み
- 実装基盤: C# / .NET 10。`Simulation.Core`、`Simulation.App`、`Simulation.Runner`、各Testsを分離
- 外部依存: test-onlyのFsCheck 3.3.4。Production Core / App / Runnerは外部packageなし

v0.15の個体生態系、v0.2の社会系、v0.2.1の5×5 Hotspotを維持する。v0.2.3では同一Active Settlement所属の両親から生まれる子を場所に関係なく同所属とし、片親所属は両親が所属先Influence内にいる場合の同Influence出生、異所属は一意なCore条件とする。Coreは5×5で、既存Influenceを新規Hotspot / Coreから保護する。Settlement詳細、60色再利用palette、NPCキル数を観測UIへ追加し、Observer / NPC単位で分離可能な読取・計画処理だけを決定論的に並列化した。Windows Formsは交換可能なPresentation Adapterであり、GUIはRealityの権威を持たない。

## Source of truth

| 情報 | 正式な置き場所 |
| --- | --- |
| 現在のゲーム仕様 | [`docs/design/`](docs/design/) |
| モジュール境界と依存方向 | [`docs/architecture/`](docs/architecture/) |
| 重要な判断と採用理由 | [`docs/decisions/`](docs/decisions/) |
| 未採用・保留・却下案 | [`docs/ideas/BACKLOG.md`](docs/ideas/BACKLOG.md) |
| 調整値・プリセット・ゲームデータ | [`simulation/`](simulation/) |
| 保存対象の実験結果 | [`research/`](research/) |
| 実行時の生ログ | `logs/`（Git管理外） |

チャットは議論の場であり、その全文は仕様ではありません。採用した結論だけを関連文書と、必要ならADRへ反映してから実装します。文書間の矛盾は、推測で片方を選ぶのではなく修正すべき不整合です。

文書の入口は [`docs/INDEX.md`](docs/INDEX.md) です。

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

成果物は `outputs\World Sim v0.2.3\Simulation.App.exe` と `outputs\World Sim v0.2.3\tools\Simulation.Runner\Simulation.Runner.exe`。同梱Configを使い、ログはreleaseごとに `logs/v0.2.3/world-NNNN/`、完了後はZIPへ保存する。

headless実行と決定論的replay:

```powershell
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build -- --headless --ticks 120 --seed 8147291
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- record --config simulation\configs\v0-default.json --seed 8147291 --ticks 120 --output work\replays\v0.2.3-seed-8147291.json
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- verify --replay work\replays\v0.2.3-seed-8147291.json
```

Appは世界生成、明示的な世界完了、指定年数×World回数の自動実行、1/2/3/5/10/50倍の速度制御、Settlement / Core / Influence / Invasion Map、ConceptMark旗、NPC詳細・キル数、移動以外の行動履歴、Settlement詳細・Friction、人口・所属率・平均年齢graph、社会・戦闘・繁殖・死因・年齢分布診断を提供する。

## Next validation

固定seed実験でGeneration→Order前後、Settlement形成率、所属率、Settlement出生、個人暴力からFrictionへの移行、CrowdingとInvasion、Auraの生態系影響を比較する。永続snapshot、国家、占領統治、反乱等はv0.2.3へ先取りしない。
