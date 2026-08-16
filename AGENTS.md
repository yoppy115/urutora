# Project Instructions

このファイルはCodexの作業規則だけを定める。ゲームの現行仕様は `docs/`、実験値は `simulation/` を正史とし、チャットログや口頭の案を自動的に仕様扱いしない。

## Before changing anything

1. `README.md` と `docs/INDEX.md` を読む。
2. 変更対象に対応する `docs/design/` の文書を読む。
3. `docs/architecture/` と関連ADRを確認する。
4. `Draft` や `TBD` を推測で埋めない。不足する判断を明示する。
5. 文書間に矛盾があれば、実装で片方を黙って選ばず報告する。

## Design principles

- システムをモジュール化し、疎結合に保つ。
- 巨大な `GameManager` や全状態を握る万能クラスを作らない。
- Reality（客観状態）とNPC Perception（主観認識）を別のデータ層にする。
- NPCの判断は主観情報だけを使い、非公開のRealityデータを直接読まない。
- Utility AIは実行可能な上位2〜3候補から、Utilityを重みにして確率選択する。
- 確率処理には注入可能なseed付き乱数源を使い、再現に必要なseedを保存する。
- 内部数値を自動的にプレイヤーUIへ露出しない。
- シミュレーションは表示層やゲームエンジンなしでも実行・テスト可能にする。
- interface、event、data-driven configuration、交換可能なmoduleを優先する。
- 無関係なシミュレーションシステム間の直接依存を避ける。

## Change contract

- 原則として安定版の `main` に直接機能実装せず、作業ブランチで差分を作る。
- 現行仕様を変える場合は、同じ変更で対応する設計文書を更新する。
- アーキテクチャ上重要、または後戻りしづらい判断にはADRを追加する。
- シミュレーション変更には、実用的な範囲で決定論的テストを追加する。
- ランダム挙動のテストではseedを固定し、失敗時にseedを表示する。
- 調整値とゲームデータはコードへ埋め込まず、設定またはデータファイルへ分離する。
- 新しいゲームエンジン、言語ランタイム、外部依存は、採用理由を示して合意後に追加する。
- 生ログをGitへ追加しない。保存価値のある実験だけを `research/` に要約して残す。

## Documentation and history

- 文書は日本語を基本とし、コード識別子とファイル形式上のキーは英語を基本とする。
- ADRは `docs/decisions/ADR-NNNN-short-title.md` 形式で連番にする。
- コミット接頭辞は内容に応じて `feat:`, `sim:`, `balance:`, `design:`, `fix:`, `refactor:`, `test:`, `docs:`, `chore:` を使う。
- Gitログは開発変更の履歴、`logs/` は世界内イベントの実行ログとして混同しない。

## Completion checklist

- 関連文書とADRに矛盾がない。
- テストまたは妥当な検証を実行し、結果を報告した。
- 設定・seed・再現手順が、変更の性質に応じて保存されている。
- 未決事項を実装上の既成事実にしていない。

