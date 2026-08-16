# Ideas Backlog

ここは正史ではありません。保留、探索中、却下した案を、理由と一緒に失わないための場所です。実装指示として扱わず、採用時に設計文書へ昇格させます。

## On hold

### NPCの思考ラグを能力と接続する

- Status: On hold
- Why it is interesting: 情報遅延とは別に、判断頻度の差が逸脱を生む可能性がある。
- Why it is not in the current design: tickと意思決定間隔が未決で、能力との重複も整理できていない。
- Conditions for reconsideration: `SIMULATION_TICK.md` とUtilityのdecision intervalを決める時。

## Exploring

### 上位存在が自然淘汰を歪める範囲

- Status: Exploring
- Adopted subset: ピン止めされた重要人物が、親和性と少量の乱数により加護を得る可能性。
- Still open: 集団全体、出生率、突然変異率、環境へ直接作用するか。
- Related design: [`PSALM_AND_INHERITANCE.md`](../design/PSALM_AND_INHERITANCE.md)

## Rejected

### Realityの完全因果を通常プレイで随時調査する

- Status: Rejected
- Reason: 過去を掘り続ける調査ループは、次の結果を見るため時間を進めたいプレイ傾向と衝突する。
- Boundary: 開発・デバッグ用の完全Reality表示まで禁止するものではない。
- Related design: [`PLAYER_OBSERVATION.md`](../design/PLAYER_OBSERVATION.md)

## Promoted to canon

- 安定世界の成功を分析し、新しい困難または概念を次世界へ追加する。
  - [`WORLD_LIFECYCLE.md`](../design/WORLD_LIFECYCLE.md)
  - [`CONCEPTS.md`](../design/CONCEPTS.md)
- ピン、加護、本人と詩篇の矛盾。
  - [`PSALM_AND_INHERITANCE.md`](../design/PSALM_AND_INHERITANCE.md)

## Entry template

```markdown
### Idea title

- Status: On hold / Exploring / Rejected
- Added: YYYY-MM-DD
- Problem or opportunity:
- Why it is interesting:
- Why it is not in the current design:
- Conditions for reconsideration:
- Related design or ADR:
```

