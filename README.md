# World Sim（仮称）

単純な個体・概念・困難から始まり、主観を持つ住民たちの適応、誤解、歴史が、次世界の上位存在・詩篇・新たな困難へ変換されることで、世界そのものが世代を超えて進化していく観測型シミュレーションです。

プレイヤーは世界を完全支配せず、限られた啓示を投げ込み、世界から返ってくる予想外の答えを観測します。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**です。

## Current status

- フェーズ: 初回Runを反映したv0.15 Ecology Update仕様／未決3項目を除き文書化
- 実装: 初回v0 Run実施済み。v0.15は文書更新段階で、今回はコード未変更
- 実装基盤: C# / .NET。`Simulation.Core`、`Simulation.App`、`Simulation.Core.Tests` を分離する
- 外部依存: なし

次段階では、v0.15の未決事項を企画側で確定してから、同一seedで再現可能なheadless Coreと観測用Desktop Applicationへ反映します。UI frameworkは未決ですが、GUIはRealityの権威を持ちません。

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
- 残す価値のある実験だけ、設定・要約・注目イベント・所見を `research/` に保存する。

## Next decisions

1. Vitality Control Point値、Held Information Eviction、Targeted Action内部順を企画側で決める。
2. v0.15文書をC#型、interface、Config schemaへ反映する。
3. 追加headless testsと必須Run metricsを実装する。
4. InitialPopulation 200から再実行し、生態系の因果を比較する。
