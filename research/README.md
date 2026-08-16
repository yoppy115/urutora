# Research Runs

通常の生ログは `logs/` に置き、Gitでは管理しません。比較・再現・設計判断に価値がある実験だけを、要約してここへ保存します。

## Suggested layout

```text
research/
  run-001-short-name/
    config.json
    summary.json
    notable-events.jsonl
    NOTES.md
```

## Proposed minimum contents

- `config.json`: 実行時の完全な設定、preset、seed、commit hash。
- `summary.json`: 主要な集計値と終了理由。
- `notable-events.jsonl`: 仮説に関係する少数のイベント。
- `NOTES.md`: 仮説、観察、解釈、次に試すこと。

巨大な全イベント列、キャッシュ、動画、クラッシュダンプは原則コミットしない。必要なら外部保存場所とchecksumだけを `NOTES.md` に記録する。

## Naming

`run-NNN-short-name` を基本とし、連番はこのリポジトリ内で重複させない。
