# World Sim（仮称）

単純な個体・概念・困難から始まり、主観を持つ住民たちの適応、誤解、歴史が、次世界の上位存在・詩篇・新たな困難へ変換されることで、世界そのものが世代を超えて進化していく観測型シミュレーションです。

プレイヤーは世界を完全支配せず、限られた啓示を投げ込み、世界から返ってくる予想外の答えを観測します。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**です。

## Current status

- フェーズ: Simulation v0.15実装・観測実験
- 実装: headless Core、Desktop観測App、自動テストを実装済み
- 実装基盤: C# / .NET 10。`Simulation.Core`、`Simulation.App`、`Simulation.Runner`、各Testsを分離
- 外部依存: test-onlyのFsCheck 3.3.4。Production Core / App / Runnerは外部packageなし

Desktop AppはWindows FormsをPresentation Adapterとして使用する。GUIはRealityの権威を持たず、同一seedのCore結果はrender頻度から独立する。

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
- 通常の巨大ログは `logs/` に出力し、完了Worldは検証済みZIPへ圧縮してGitへコミットしない。
- 残す価値のある実験だけ、設定・要約・注目イベント・所見を `research/` に保存する。

## Build and run

.NET 10 SDKとWindowsが必要。初回test build時はFsCheck取得のため公式NuGetへの接続が必要。

```powershell
.\build.ps1 -RunTests
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build
```

配布用EXEを生成する場合:

```powershell
.\publish.ps1
```

成果物は `outputs\World Sim v0.15\Simulation.App.exe` と `outputs\World Sim v0.15\tools\Simulation.Runner\Simulation.Runner.exe` に生成される。`World Sim v0.15` フォルダ全体を保持すれば、リポジトリ外へ移動してもAppは同梱Configで起動し、同フォルダ配下の `logs/` へWorldログを保存する。

headlessで同じCoreを実行する場合:

```powershell
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build -- --headless --ticks 100 --seed 8147291
```

決定論的replayを記録・照合する場合:

```powershell
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- record --config simulation\configs\v0-default.json --seed 8147291 --ticks 100 --output work\replays\v0.15-seed-8147291.json
dotnet run --project src\Simulation.Runner\Simulation.Runner.csproj -c Release --no-build -- verify --replay work\replays\v0.15-seed-8147291.json
```

GitHub Actionsはpush、pull request、手動実行でWindows/.NET 10 build、Core/App/Runner test、10 tick replay smokeを行う。

Simulation Configは `simulation/configs/v0-default.json`、観測App Configは `simulation/configs/observation-app.json`。Appは世界生成、Pause、1日進行、通常・高速・Max速度、Map、Year / Day、主要Event、NPCクリック詳細、移動を除くNPC行動履歴、人口・平均年齢graph、選択Action集計、死因、現在年齢分布、およびv0.15の繁殖・TargetAbsent・Combat・Perception・Concept診断表を提供する。graphは表示期間、開始比、観測範囲、現在値を表示し、pointerを重ねると日別の正確な値と変化量を確認できる。

生成Worldはreleaseごとに `world-0001` から番号付けされ、観測中は`logs/vX.Y/world-NNNN/`にrun metadata、使用Config、`events.jsonl`、`daily-stats.csv`、`diagnostics.jsonl`を保存する。完了後は`world-NNNN.zip`とSHA-256 sidecarへ圧縮し、ZIPも連番へ含める。現行release以外のlog directoryはApp Configに従って削除する。これらは生ログのためGit管理外である。

ログ整理だけを明示実行する場合:

```powershell
dotnet run --project src\Simulation.App\Simulation.App.csproj -c Release --no-build -- --maintain-logs --repository-root .
```

`publish.ps1`はclean Git worktreeをrelease条件とし、commit・tree state・成果物SHA-256を`release-manifest.json`へ保存する。dirtyな診断buildだけが必要な場合は`-AllowDirty`を明示する。

## Next validation

固定seed実験で、空間競合、誤認、情報変形、淘汰が「次を見たい」逸脱を生むか観測する。永続化、世界再編、詩篇等のDraftはv0へ先取りしない。
