# World Sim（仮称）

単純な個体・概念・困難から始まり、主観を持つ住民たちの適応、誤解、歴史が、次世界の上位存在・詩篇・新たな困難へ変換されることで、世界そのものが世代を超えて進化していく観測型シミュレーションです。

プレイヤーは世界を完全支配せず、限られた啓示を投げ込み、世界から返ってくる予想外の答えを観測します。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**です。

## Current status

- フェーズ: v0.2.5 Knowledge, Fission & Invasion UpdateおよびUnresolved Contracts Closure正史確定
- 実装: v0.2.4までの既存実装・正史を基礎に、v0.2.5は正史文書確定・コード未変更
- 実装基盤: C# / .NET。`Simulation.Core`、`Simulation.App`、`Simulation.Core.Tests` を分離する
- 外部依存: なし

次段階では、v0.2.4までの境界を維持しながら、v0.2.5の三種Knowledge、増分統計、累積Support / Renewal、Fission先行、持続型Invasion、今回確定した記憶・Center・Migration契約を同一seedで再現可能なCoreと観測Appへ反映します。

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
- 残す価値のある実験だけ、設定・要約・注目イベント・所見を `research/` に保存する。
- Run比較ではVersionだけでなく`repositoryCommit`、Config、Seedを併記する。

## Next decisions

1. v0.2.5文書をC#型、interface、Config schemaへ反映する。
2. Knowledge / Memory、Event retention、Statistics、Support / Renewal / Fission、Invasion participant stateを既存責務へ分離して実装する。
3. v0.2.5 headless testsと増分Statistics projectionを実装する。
4. 同一seedでv0.2.4とv0.2.5を比較し、記憶、平和的拡張、戦線、Settlement寿命の因果を観測する。
