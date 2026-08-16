# Logging

**Status:** Baseline constraints / Draft mechanics

## Baseline purpose

世界の再現、デバッグ、ピン抽出、歴史・詩篇生成のため、機械可読なログを保持可能な構造にする。

最低限、次の論理カテゴリを扱う。

- Reality Log
- Perception Log
- Decision Log
- Action Log
- Birth / Death Log
- Revelation Log
- Pin Log
- Inheritance Log

一つのイベントが複数カテゴリから参照されてもよい。ログカテゴリを必ず別ファイルへ分割する必要はない。

## Baseline boundaries

- ログは世界の結果を変更しない。
- 生ログすべてをGitへ保存しない。
- 保存価値のあるrunだけ、設定、seed、commit、要約、注目イベントを `research/` に残す。
- ピンと詩篇入力は、元ログの安定IDを参照できるようにする。
- Perception LogはReality Logの単なる複製にしない。

## Draft mechanics

- JSONL、binary、database等の保存形式。
- schema versionとmigration。
- snapshotとevent logの分担。
- 保存期間、圧縮、sampling、個体数増加時の性能。
- Perceptionや関係情報の記録粒度。
- 詩篇生成へ渡す情報の選別と秘匿境界。

