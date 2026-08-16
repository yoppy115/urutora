# Concepts

**Status:** Draft

## Goal

「概念」をコードに直書きせず、データとして追加・調整できるようにする。概念追加のたびにプログラム本体の分岐を増やさない。

## Candidate concepts

元の会話では、候補として次が例示された。名称・意味・採用はまだ確定していない。

- 闘争
- 生存
- 交流
- 扶助
- 統合

## Provisional data shape

以下はスキーマ案であり、現行データではない。

```yaml
id: struggle
name: 闘争
parameters:
  action: 0.5
  combat: 0.5
difficulty:
  id: brutality
  name: 暴戻
```

実データは将来 `simulation/concepts/` に置く。

## Required decisions

- 概念がNPC、世界、プレイヤー介入のどこへ作用するか。
- `parameters` の共通スキーマと型。
- 概念同士の競合・合成・進化の規則。
- 難易度データの意味と、概念との多重度。
- 保存データで参照する安定IDと、表示名のローカライズ方法。

