# World Sim（仮称）

自律的に変化する世界へプレイヤーが限定的に介入し、その結果を観測・解釈するゲーム企画です。このリポジトリはコード置き場だけではなく、人間・ChatGPT・Codexが共有する**開発上の正史**として扱います。

## Current status

- フェーズ: 設計リポジトリ初期化済み／ゲーム企画の正史移植は未完了
- 実装: 未着手
- ゲームエンジン・言語・テスト基盤: 未決定
- 外部依存: なし
- 正史移植: 添付されたGit運用会話内の明示事項のみ

まだゲームエンジンは導入しません。まず仕様の境界と未決事項を明確にし、ヘッドレスで検証できる最小シミュレーションへ進みます。

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

チャットは議論の場であり、その全文は仕様ではありません。採用した結論だけを関連文書と、必要ならADRへ反映してから実装します。文書間の矛盾は「どちらかを推測してよい」という意味ではなく、修正すべき不整合です。

文書の入口は [`docs/INDEX.md`](docs/INDEX.md) です。

## Development flow

1. `main` を現在の安定した正史として扱う。
2. 変更ごとに作業ブランチを作る。例: `feat/utility-ai`, `experiment/perception-delay`。環境が接頭辞を要求する場合はその規則を優先する。
3. 関連設計書・アーキテクチャ・ADRを読んでから変更する。
4. 実装、テスト、文書更新を同じ差分へ含める。
5. Pull Requestまたはローカル差分でレビューしてから `main` へ統合する。

コミット例:

```text
design: define primordial concepts
sim: add age-based hp decay
fix: prevent agents reading hidden reality state
refactor: isolate perception from utility evaluation
```

## Reproducible experiments

- すべての確率的な実行で乱数seedを保存する。
- 調整値はコードから `simulation/configs/` へ分離する。
- 通常の巨大ログは `logs/` に出力し、Gitへコミットしない。
- 残す価値のある実験だけ、設定・要約・注目イベント・所見を `research/` に保存する。

## Next decisions

1. 元のゲーム企画会話から、未確定の設計書へ採用済み仕様だけを移す。
2. 世界時間と1サイクルの処理順を決める。
3. 概念データ、知覚、繁殖の最小スキーマを決める。
4. ゲームエンジンより先に、シミュレーションコアの言語とテスト方法を選ぶ。
5. 固定seedで動く最小のヘッドレス・シミュレーションを実装する。
