# ADR-0010: v0 separates C# Core, App, and headless tests

- **Status:** Accepted
- **Date:** 2026-08-16

## Context

高速な実験と自動テストに加え、人間がNPCを観測できるDesktop Applicationが必要である。UI loopがSimulation結果へ影響してはならない。

## Decision

v0の言語とruntimeをC# / .NETとし、概念上 `Simulation.Core`、`Simulation.App`、`Simulation.Core.Tests` を分ける。CoreだけがRealityの権威を持ち、AppとTestsがCoreへ依存する。render更新とtickを分離し、Appはread-only projectionとEvent streamを表示する。UI frameworkは実装時に選択する。

## Consequences

- CoreはGUIなしで高速実行・テスト可能でなければならない。
- render頻度を変えた決定性テストが必要になる。
- UI固有型をCore APIへ持ち込まない。

## Implementation extension (2026-08-17)

決定論的replayの記録・照合用に `Simulation.Runner` と `Simulation.Runner.Tests` をCore外側へ追加した。これは新しいSimulation規則ではなく、Accepted済みの `App/Tests -> Core` 依存方向を `App/Runner/Tests -> Core` へ拡張するadapterである。CoreのReality権威とGUI分離は変更しない。
